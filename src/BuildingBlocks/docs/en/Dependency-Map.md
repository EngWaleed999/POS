# SuperMarket.BuildingBlocks — Dependency Map

> **Scope:** Dependency direction, external packages, internal component relationships, DI registrations, and architectural coupling analysis.

---

## 1. High-Level Dependency Graph

The internal layering within `SuperMarket.BuildingBlocks` enforces unidirectional dependency flow:

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

### Dependency Rules:
1. **`Domain`** has **zero dependencies** on `Application` or `Infrastructure`. It depends solely on `MediatR.Contracts` (via MediatR NuGet) for `INotification`.
2. **`Results`** has **zero dependencies** on `Domain` or `Application`. It depends only on basic ASP.NET Core HTTP abstractions for `IResult` / RFC 7807 generation.
3. **`Application`** depends on `Domain` and `Results`.
4. **`Infrastructure`** depends on `Domain`, `Application`, and `Results`.

---

## 2. External Package Dependencies (.csproj)

Inspecting `src/BuildingBlocks/SuperMarket.BuildingBlocks/SuperMarket.BuildingBlocks.csproj`:

| Package / Reference | Version | Target Layer | Purpose in BuildingBlocks |
| :--- | :--- | :--- | :--- |
| **`FluentValidation`** | `11.11.0` | `Application` | Command/Query input validation in `ValidationPipelineBehavior`. |
| **`MediatR`** | `12.4.1` | `Application`, `Domain`, `Infrastructure` | In-process mediator, CQRS markers (`IRequest`), pipeline behaviors (`IPipelineBehavior`), and domain events (`INotification`, `IPublisher`). |
| **`Microsoft.EntityFrameworkCore`** | `10.0.3` | `Infrastructure`, `Application` | Interceptor hooks (`SaveChangesInterceptor`), ChangeTracker inspection, `IQueryable` async pagination execution (`CountAsync`, `ToListAsync`). |
| **`Microsoft.AspNetCore.App`** (FrameworkReference) | Built-in | `Infrastructure`, `Results` | Centralized exception handling (`IExceptionHandler`), ProblemDetails (`IProblemDetailsService`, `Results.Problem`), and HttpContext access. |

---

## 3. Downstream Microservices Consumption

`SuperMarket.BuildingBlocks` is consumed as a direct ProjectReference across all bounded contexts in the solution:

```text
SuperMarket.BuildingBlocks
    ▲
    │
    ├── SuperMarket.Identity (.Domain, .Application, .Infrastructure)
    ├── SuperMarket.Inventory (.Domain, .Application, .Infrastructure)
    ├── SuperMarket.Sales (.Domain, .Application, .Infrastructure)
    └── SuperMarket.Operations (.Domain, .Application, .Infrastructure)
```

### Typical Usage per Downstream Layer:
* **Service.Domain:** Inherits from `AggregateRoot<TId>`, `Entity<TId>`, `ValueObject`, and implements `IAuditableEntity`, `ISoftDeletable`.
* **Service.Application:** Implements `ICommand`, `IQuery`, `ICommandHandler`, and consumes `PagedList<T>`, `Result<T>`.
* **Service.Infrastructure:** Configures `AuditSaveChangesInterceptor`, `DispatchDomainEventsInterceptor`, and calls `modelBuilder.ApplySoftDeleteQueryFilter()`.
* **Service.Api:** Calls `builder.Services.AddBuildingBlocksWeb()` and `app.UseBuildingBlocksWeb()`.

---

## 4. Dependency Injection Registrations

### 4.1 Centralized Extension Method (`Infrastructure/DependencyInjection.cs`)
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

### 4.2 Interceptor Registration Pattern (Configured in Downstream Services)
Because `AuditSaveChangesInterceptor` and `DispatchDomainEventsInterceptor` operate on service-specific `DbContext` instances, they are registered in the DI container per service:
```csharp
// Example in Service Infrastructure:
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

### 4.3 Pipeline Behaviors Registration
MediatR behaviors are registered during downstream MediatR service configuration:
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
    cfg.AddOpenBehavior(typeof(PerformancePipelineBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
});
```

---

## 5. Dependency Risks & Architectural Analysis

### Risk 1: EF Core Dependency in `Application/PagedList.cs`
* **Observation:** `PagedList<T>.CreateAsync` directly calls `Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync` and `ToListAsync`.
* **Architectural Impact:** In pure Clean Architecture theory, the Application layer should remain agnostic of the underlying ORM. 
* **Evaluation & Justification:** For an internal Modular Monolith, abstracting `CountAsync` and `ToListAsync` behind a custom wrapper adds unnecessary complexity (Over-Engineering) without tangible benefits, as EF Core is the standardized ORM across the entire solution. However, consumers must be aware that using `PagedList.CreateAsync` requires an EF Core `IQueryable` provider.

### Risk 2: Monolithic Single Project Packaging
* **Observation:** Domain, Application, Infrastructure, and Results are compiled into a single assembly (`SuperMarket.BuildingBlocks.dll`).
* **Architectural Impact:** A service's Domain project referencing `SuperMarket.BuildingBlocks` technically has access to `Infrastructure` classes (e.g. `AuditSaveChangesInterceptor`) and ASP.NET Core references.
* **Evaluation & Justification:** For a fast-moving, cohesive team working on a single solution repository, maintaining 4 separate BuildingBlock sub-projects (`BuildingBlocks.Domain`, `BuildingBlocks.Application`, etc.) adds project reference friction and maintenance overhead. Architectural discipline and code review must ensure that service Domain projects do not consume Infrastructure classes.
