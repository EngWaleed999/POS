# خريطة التبعيات لمكتبة SuperMarket.BuildingBlocks

> **النطاق:** توثيق اتجاه التبعيات، الحزم الخارجية، العلاقات بين المكونات، تسجيلات حاوية الـ DI، وتحليل المخاطر المعمارية.

---

## 1. مخطط التبعيات عالي المستوى (High-Level Dependency Graph)

تفرض الهيكلية الداخلية لمكتبة `SuperMarket.BuildingBlocks` اتجاه تبعيات أحادي وصارم:

```text
┌─────────────────────────────────────────────────────────────┐
│                       Infrastructure                        │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
┌───────────────────────────────┐     ┌────────────────────────┐
│          Application          │────►│        Results         │
└──────────────┬────────────────┘     └────────────────────────┘
               │                               ▲
               ▼                               │
┌───────────────────────────────┐              │
│            Domain             │──────────────┘
└───────────────────────────────┘
```

### قواعد التبعيات الصارمة:
1. **طبقة `Domain`:** تملك **صفر تبعيات** على طبقتي `Application` و `Infrastructure`. تعتمد فقط على مجردات `MediatR` (واجهة `INotification`).
2. **طبقة `Results`:** تملك **صفر تبعيات** على طبقتي `Domain` و `Application`. تعتمد فقط على واجهات ASP.NET Core الأساسية لدعم إخراج معيار RFC 7807.
3. **طبقة `Application`:** تعتمد على `Domain` و `Results`.
4. **طبقة `Infrastructure`:** تعتمد على جميع الطبقات السابقة (`Domain`, `Application`, `Results`).

---

## 2. الحزم الخارجية وحزم الـ NuGet (.csproj)

بفحص ملف المشروع `SuperMarket.BuildingBlocks.csproj`:

| الحزمة / المرجع | الإصدار | الطبقة المستهدفة | الغرض المعماري من الاستخدام |
| :--- | :--- | :--- | :--- |
| **`FluentValidation`** | `11.11.0` | `Application` | التحقق من صحة مدخلات الـ Commands والـ Queries داخل `ValidationPipelineBehavior`. |
| **`MediatR`** | `12.4.1` | `Application`, `Domain`, `Infrastructure` | وسيط الذاكرة الداخلي، واجهات CQRS، سلوكيات الـ Pipeline، وأحداث الدومين (`INotification`, `IPublisher`). |
| **`Microsoft.EntityFrameworkCore`** | `10.0.3` | `Infrastructure`, `Application` | خطافات الاعتراض (`SaveChangesInterceptor`)، وتتبع الكيانات، وتنفيذ استعلامات الترقيم (`CountAsync`, `ToListAsync`). |
| **`Microsoft.AspNetCore.App`** (FrameworkReference) | مدمج في .NET | `Infrastructure`, `Results` | المعالجة المركزية للاستثناءات (`IExceptionHandler`)، وإنتاج ProblemDetails، والوصول لسياق الطلب. |

---

## 3. استهلاك الخدمات التابعة للمكتبة (Downstream Services Consumption)

يتم استهلاك `SuperMarket.BuildingBlocks` كمرجع مشروع مباشر عبر كافة الـ Bounded Contexts في المنظومة:

```text
SuperMarket.BuildingBlocks
    ▲
    │
    ├── SuperMarket.Identity (.Domain, .Application, .Infrastructure)
    ├── SuperMarket.Inventory (.Domain, .Application, .Infrastructure)
    ├── SuperMarket.Sales (.Domain, .Application, .Infrastructure)
    └── SuperMarket.Operations (.Domain, .Application, .Infrastructure)
```

### طبيعة الاستهلاك في كل طبقة:
* **طبقة Domain في الخدمات:** ترث من `AggregateRoot<TId>` و `Entity<TId>` و `ValueObject` وتطبق واجهات القدرات (`IAuditableEntity`, `ISoftDeletable`).
* **طبقة Application في الخدمات:** تطبق `ICommand` و `IQuery` وتتعامل مع النتائج عبر `Result<T>` والترقيم عبر `PagedList<T>`.
* **طبقة Infrastructure في الخدمات:** تهيئ `AuditSaveChangesInterceptor` و `DispatchDomainEventsInterceptor` وتفعل الفلتر العام عبر `ApplySoftDeleteQueryFilter()`.
* **طبقة API في الخدمات:** تستدعي دوال التهيئة `AddBuildingBlocksWeb()` و `UseBuildingBlocksWeb()`.

---

## 4. تسجيل التبعيات في الـ DI Container

### 4.1 دالة التوسعة المركزية (`Infrastructure/DependencyInjection.cs`)
```csharp
public static IServiceCollection AddBuildingBlocksWeb(this IServiceCollection services)
{
    services.AddExceptionHandler<GlobalExceptionHandler>();
    services.AddProblemDetails();
    return services;
}

public static IApplicationBuilder UseBuildingBlocksWeb(this IApplicationBuilder app)
{
    app.UseExceptionHandler();
    return app;
}
```

### 4.2 تسجيل المراقبين في الخدمات التابعة
نظراً لأن المراقبين يعملون مع كل `DbContext` خاص بالخدمة، يتم تسجيلهم بنطاق `Scoped` في مشروع الخدمة:
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

## 5. تحليل المخاطر المعمارية والتقييم الهندسي

### الخطر الأول: وجود تبعية EF Core داخل طبقة `Application/PagedList.cs`
* **الملاحظة البرمجية:** يستدعي `PagedList<T>.CreateAsync` دوال `CountAsync` و `ToListAsync` التابعة لـ `Microsoft.EntityFrameworkCore` مباشرة.
* **الأثر المعماري:** من الناحية النظرية الصارمة لـ Clean Architecture، يجب أن تظل طبقة الـ Application خالية من أي مكتبة ORM ملموسة.
* **التقييم والقرار:** في نظام Modular Monolith يعتمد EF Core كمعيار رسمي وحيد، فإن إنشاء تجريد معقد حول استعلامات الـ Async IQueryable يعد تعقيداً مفرطاً (Over-Engineering) بلا فائدة عملية. مع ذلك، يجب أن يدرك المطور أن `PagedList.CreateAsync` يتطلب سياق استعلام يدعم EF Core.

### الخطر الثاني: تجميع المكتبة في مشروع واحد (Single Assembly)
* **الملاحظة البرمجية:** تم تجميع الـ Domain والـ Application والـ Infrastructure والـ Results في ملف DLL واحد (`SuperMarket.BuildingBlocks.dll`).
* **الأثر المعماري:** تملك مشاريع الـ Domain في الخدمات التابعة إمكانية الوصول النظري لكلاسات البنية التحتية ومكتبات الويب.
* **التقييم والقرار:** بالنسبة لفريق عمل واحد في مستودع موحد، فإن إدارة 4 مشاريع فرعية لـ BuildingBlocks يرفع تعقيد صيانة المراجع. الاعتماد على الانضباط المعماري والمراجعة البرمجية (Code Review) يمنع أي تسرب بين الطبقات بكفاءة عالية وبأقل تكلفة صيانة.
