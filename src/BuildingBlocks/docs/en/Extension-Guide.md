# SuperMarket.BuildingBlocks — Developer Extension Guide

> **Scope:** Step-by-step practical workflows for safely extending, consuming, and maintaining components based on `SuperMarket.BuildingBlocks`.

---

## 1. How to Define a Domain Aggregate & Raise Domain Events

1. Inherit your root entity from `AggregateRoot<TId>`.
2. Apply capability interfaces (`IAuditableEntity`, `ISoftDeletable`, `IActivatable`) as required.
3. Create domain events inheriting from `DomainEvent`.
4. Call `AddDomainEvent(new YourEvent(...))` inside domain methods.

### Code Example:
```csharp
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Sales.Domain;

public sealed record ShiftClosedDomainEvent(Guid ShiftId, Guid CashierId, decimal TotalCash) 
    : DomainEvent;

public sealed class CashierShift : AggregateRoot<Guid>, IAuditableEntity, ISoftDeletable
{
    public Guid CashierId { get; private set; }
    public decimal TotalCash { get; private set; }
    public bool IsClosed { get; private set; }

    // IAuditableEntity implementation
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // ISoftDeletable implementation
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    private CashierShift() { } // Required for EF Core

    public CashierShift(Guid id, Guid cashierId) : base(id)
    {
        CashierId = cashierId;
        IsClosed = false;
    }

    public void CloseShift(decimal actualCash)
    {
        TotalCash = actualCash;
        IsClosed = true;

        // Raise domain event to be dispatched on SaveChangesAsync
        AddDomainEvent(new ShiftClosedDomainEvent(Id, CashierId, actualCash));
    }
}
```

---

## 2. How to Add a New Error & HTTP Mapping

1. If you need a new error category (e.g. `PaymentRequired = 6`), add it to `Results/ErrorType.cs`.
2. Add a static factory method in `Results/Error.cs`.
3. Update `Results/ResultProblemDetailsExtensions.cs` to map the status code (e.g. `402 Payment Required`).

### Code Example:
```csharp
// In Error.cs:
public static Error PaymentRequired(string code, string description) =>
    new(code, description, ErrorType.PaymentRequired);

// In ResultProblemDetailsExtensions.cs:
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

## 3. How to Implement `ICurrentUserContext` in an API Service

In your Web API project, extract the authenticated user identity (e.g. Keycloak JWT `sub` claim) from `IHttpContextAccessor`:

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
Register in DI:
```csharp
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserContext, CurrentUserContext>();
```

---

## 4. How to Configure a Service DbContext with BuildingBlocks Interceptors

In the service's `Infrastructure` project:

```csharp
public class SalesDbContext : DbContext
{
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Apply global soft delete filter to all ISoftDeletable entities
        modelBuilder.ApplySoftDeleteQueryFilter();

        // 2. Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
    }
}
```

Registering interceptors in `Program.cs` / DI:
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

## 5. How to Implement Pagination in Query Handlers

### Strategy A: Offset Pagination (Admin Screens)
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

### Strategy B: Keyset/Cursor Pagination (Cashier & High-Volume Logs)
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

        // Fetch pageSize + 1 to detect if next page exists without COUNT(*)
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

## 6. Architectural Anti-Patterns (What NOT to Do)

* ❌ **DO NOT throw exceptions for expected business failures.** Always return `Result.Failure(Error.NotFound(...))` or similar.
* ❌ **DO NOT access `Result<T>.Value` without checking `IsSuccess`.** It will throw `InvalidOperationException`. Use `.Match(...)` or check `if (result.IsFailure)`.
* ❌ **DO NOT manually write `WHERE is_deleted = false` in EF queries.** The global query filter handles this automatically.
* ❌ **DO NOT attempt to modify `CreatedAt` or `CreatedBy` on entity updates.** `AuditSaveChangesInterceptor` marks them unmodified and discards changes.
* ❌ **DO NOT inject `IPublisher` or `DbContext` into Domain Entities.** Keep the domain pure. Entities only record events into their internal collection via `AddDomainEvent()`.
* ❌ **DO NOT execute unbound queries without pagination parameters.** Always use clamped `PaginationParams` or `CursorParams` to prevent denial-of-service memory pressure.
