# دليل التطوير والتوسيع الهندسي (Developer Extension Guide)

> **النطاق:** إرشادات عملية وخطوات واضحة للمطورين لتوسيع واستخدام مكونات `SuperMarket.BuildingBlocks` بأمان وبما يتوافق مع المعايير المعمارية للمشروع.

---

## 1. كيفية تعريف جذر تجميع (Domain Aggregate) وإطلاق أحداث الدومين

1. اجعل الكيان يرث من كلاس `AggregateRoot<TId>`.
2. طبّق واجهات القدرات المطلوبة (`IAuditableEntity`, `ISoftDeletable`, `IActivatable`).
3. عرّف سجلات الأحداث التي ترث من كلاس `DomainEvent`.
4. استدعِ دالة `AddDomainEvent(...)` داخل دوال تعديل حالة الكيان في الدومين.

### مثال برمجي متكامل:
```csharp
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Sales.Domain;

// 1. تعريف حدث الدومين كسجل غير قابل للتعديل
public sealed record ShiftClosedDomainEvent(Guid ShiftId, Guid CashierId, decimal TotalCash) 
    : DomainEvent;

// 2. بناء جذر التجميع وتطبيق واجهات القدرات
public sealed class CashierShift : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable
{
    public Guid CashierId { get; private set; }
    public decimal TotalCash { get; private set; }
    public bool IsClosed { get; private set; }

    // حقول واجهة IAuditableEntity
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // حقول واجهة ISoftDeletable
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    private CashierShift() { } // مطلوب لـ EF Core

    public CashierShift(Guid id, Guid cashierId) : base(id)
    {
        CashierId = cashierId;
        IsClosed = false;
    }

    public void CloseShift(decimal actualCash)
    {
        TotalCash = actualCash;
        IsClosed = true;

        // إطلاق حدث الدومين ليتم نشره تلقائياً عند استدعاء SaveChangesAsync
        AddDomainEvent(new ShiftClosedDomainEvent(Id, CashierId, actualCash));
    }
}
```

---

## 2. كيفية إضافة نوع خطأ جديد وترجمته إلى HTTP ProblemDetails

1. إذا تطلب النظام تصنيفاً جديداً (مثل `PaymentRequired = 6`)، أضفه في ملف `Results/ErrorType.cs`.
2. أضف دالة مصنع ثابتة داخل كلاس `Results/Error.cs`.
3. حدد كود الاستجابة القياسي (مثل `402 Payment Required`) داخل `Results/ResultProblemDetailsExtensions.cs`.

### مثال تطبيقي:
```csharp
// داخل ملف Error.cs:
public static Error PaymentRequired(string code, string description) =>
    new(code, description, ErrorType.PaymentRequired);

// داخل ملف ResultProblemDetailsExtensions.cs:
var statusCode = error.Type switch
{
    ErrorType.Validation => StatusCodes.Status400BadRequest,
    ErrorType.NotFound => StatusCodes.Status404NotFound,
    ErrorType.Conflict => StatusCodes.Status409Conflict,
    ErrorType.PaymentRequired => StatusCodes.Status402PaymentRequired,
    // ...
};
```

---

## 3. كيفية تطبيق `ICurrentUserContext` في خدمة الـ Web API

في مشروع الـ Infrastructure الخاص بالخدمة المستضيفة، يتم استخراج هوية المستخدم المسجل (من Keycloak JWT Claims) عبر `IHttpContextAccessor`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SuperMarket.BuildingBlocks.Infrastructure;

namespace SuperMarket.Sales.Infrastructure;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
}
```
تسجيله في حاوية الـ DI:
```csharp
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserContext, CurrentUserContext>();
```

---

## 4. تهيئة `DbContext` الخدمة مع مراقبات BuildingBlocks والفلتر العام

داخل مشروع الـ Infrastructure الخاص بالخدمة:

```csharp
public class SalesDbContext : DbContext
{
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. تطبيق فلتر الحذف المنطقي العام على كل الجداول المطبقة لـ ISoftDeletable
        modelBuilder.ApplySoftDeleteQueryFilter();

        // 2. تطبيق إعدادات الكيانات
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
    }
}
```

ربط المراقبين في الـ DI داخل `Program.cs`:
```csharp
services.AddScoped<AuditSaveChangesInterceptor>();
services.AddScoped<DispatchDomainEventsInterceptor>();

services.AddDbContext<SalesDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString)
           .AddInterceptors(
               sp.GetRequiredService<AuditSaveChangesInterceptor>(),
               sp.GetRequiredService<DispatchDomainEventsInterceptor>());
});
```

---

## 5. تطبيق الترقيم المزدوج داخل معالجات الاستعلام (Query Handlers)

### النمط الأول: الترقيم بالإزاحة (Offset Pagination للشاشات الإدارية)
```csharp
public sealed record GetBranchesQuery(PaginationParams Params) : IQuery<PagedList<BranchDto>>;

public sealed class GetBranchesQueryHandler : IQueryHandler<GetBranchesQuery, PagedList<BranchDto>>
{
    private readonly SalesDbContext _context;

    public async Task<Result<PagedList<BranchDto>>> Handle(GetBranchesQuery request, CancellationToken ct)
    {
        var query = _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name));

        var pagedList = await PagedList<BranchDto>.CreateAsync(
            query, 
            request.Params.PageNumber, 
            request.Params.PageSize, 
            cancellationToken: ct);

        return Result.Success(pagedList);
    }
}
```

### النمط الثاني: الترقيم بالمؤشر (Keyset / Cursor Pagination لمعاملات الكاشير)
```csharp
public sealed record GetSalesFeedQuery(CursorParams<DateTimeOffset> Params) 
    : IQuery<CursorPagedList<SaleDto, DateTimeOffset>>;

public sealed class GetSalesFeedQueryHandler 
    : IQueryHandler<GetSalesFeedQuery, CursorPagedList<SaleDto, DateTimeOffset>>
{
    private readonly SalesDbContext _context;

    public async Task<Result<CursorPagedList<SaleDto, DateTimeOffset>>> Handle(
        GetSalesFeedQuery request, CancellationToken ct)
    {
        var pageSize = request.Params.PageSize;

        var query = _context.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt);

        if (request.Params.Cursor.HasValue)
        {
            query = query.Where(s => s.CreatedAt < request.Params.Cursor.Value);
        }

        // جلب pageSize + 1 لمعرفة هل توجد صفحة تالية دون الحاجة لاستعلام COUNT(*) المكلف
        var items = await query.Take(pageSize + 1).ToListAsync(ct);

        DateTimeOffset? nextCursor = null;
        if (items.Count > pageSize)
        {
            var nextItem = items[^1];
            nextCursor = nextItem.CreatedAt;
            items.RemoveAt(items.Count - 1);
        }

        var dtos = items.Select(s => new SaleDto(s.Id, s.TotalAmount, s.CreatedAt)).ToList();
        return Result.Success(new CursorPagedList<SaleDto, DateTimeOffset>(dtos, nextCursor));
    }
}
```

---

## 6. المحاذير والممارسات المرفوضة معمارياً (Anti-Patterns)

* ❌ **لا ترمِ استثناءات (Exceptions) لأخطاء البزنس المتوقعة:** أرجع دائماً `Result.Failure(Error.NotFound(...))` أو ما يناظرها.
* ❌ **لا تحاول قراءة `Result<T>.Value` دون التأكد من `IsSuccess`:** سيؤدي ذلك لرمي `InvalidOperationException`. استخدم دوال `.Match(...)` أو تحقق مسبقاً من `if (result.IsFailure)`.
* ❌ **لا تكتب شرط `WHERE is_deleted = false` يدوياً في استعلامات EF Core:** الفلتر العام يضمن تطبيق هذا الشرط تلقائياً في كل استعلام `SELECT`.
* ❌ **لا تعدل حقلي `CreatedAt` أو `CreatedBy` أثناء التعديل (Update):** يقوم `AuditSaveChangesInterceptor` بتعطيل تعديل هذه الحقول برمجياً وحمايتها من التغيير.
* ❌ **لا تحقن `IPublisher` أو `DbContext` داخل كلاسات الدومين:** حافظ على نقاء الدومين، ودع الكيانات تكتفي بتسجيل الأحداث محلياً عبر `AddDomainEvent()`.
* ❌ **لا تنفذ استعلامات مفتوحة بدون معاملات ترقيم:** استخدم دوماً `PaginationParams` أو `CursorParams` المحمية بحدود قصوى لحماية خوادم النظام وقواعد البيانات من نفاد الذاكرة وهجمات الـ DoS.
