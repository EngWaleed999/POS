# SuperMarket.BuildingBlocks — Technical Q&A & Interview Knowledge Base

> **Scope:** In-depth architectural questions, rationale, edge cases, failure semantics, and technical interview preparation based directly on the `SuperMarket.BuildingBlocks` implementation.

---

## 1. Core Architectural Questions (Why?)

### Q1: Why use the Result Pattern instead of throwing Exceptions for business validation and domain failures?
* **Answer:** In high-concurrency systems like a POS handling hundreds of barcode scans per second, exceptions are an anti-pattern for anticipated control flow:
  1. **Performance Cost:** Throwing an exception in .NET forces the runtime to capture the call stack, walk stack frames, and allocate heavy exception objects. This can cause CPU throttling and GC pressure.
  2. **Invisible API Signatures:** A method signature like `Task<Order> CheckoutAsync(...)` conceals the fact that it might fail if stock is zero or a cashier shift is closed. A signature returning `Task<Result<Order>>` forces the caller via the type system to acknowledge and handle both success and failure outcomes.
  3. **Separation of Concerns:** Exceptions are strictly reserved for unrecoverable infrastructure crashes (e.g. database network drops, out of memory), routed to `GlobalExceptionHandler` to yield HTTP 500. Expected domain failures yield deterministic 4xx ProblemDetails via `Result.Failure`.

---

### Q2: Why use capability interfaces (`IAuditableEntity`, `ISoftDeletable`, `IActivatable`) instead of a single `BaseEntity`?
* **Answer:** Applying **Composition over Inheritance** and the **Interface Segregation Principle (ISP)** prevents the "God Object" anti-pattern:
  - If auditing and soft-deletion are baked into a single base class, tables that must never be modified or soft-deleted (such as `AuditLog`, `DailyZReport`, or append-only ledger entries) are forced to carry useless nullable columns (`DeletedAt`, `DeletedBy`, `UpdatedAt`).
  - By using capability interfaces, each domain model chooses exactly the persistence traits it requires. `AuditSaveChangesInterceptor` inspects these interfaces dynamically via EF Core's `ChangeTracker`.

---

### Q3: Why does `DispatchDomainEventsInterceptor` clear domain events BEFORE dispatching them via `_publisher.Publish`?
* **Answer:** Idempotency and recursion protection. If `root.ClearDomainEvents()` were called *after* `Publish`, and one of the domain event handlers performed an operation that triggered another `SaveChangesAsync()` within the same execution chain, the interceptor would re-read the uncleared events and publish them again, triggering an infinite recursive loop or duplicate notifications. Purging the events before publishing guarantees that each event is dispatched exactly once.

---

### Q4: Why use `TimeProvider` instead of calling `DateTime.UtcNow` directly in `AuditSaveChangesInterceptor`?
* **Answer:** Calling `DateTime.UtcNow` couples code to the system clock, making it impossible to write deterministic unit tests for time-sensitive logic (e.g., verifying that `CreatedAt` matches an exact instant). By injecting `TimeProvider` (defaulting to `TimeProvider.System`), unit tests can supply a `FakeTimeProvider` with a frozen or stepped timestamp, allowing precise assertions without fragile tolerance deltas (`TimeSpan.FromSeconds(1)`).

---

### Q5: Why implement both Offset and Keyset (Cursor) pagination?
* **Answer:** Different workloads exhibit opposite scalability characteristics:
  - **Offset (`PagedList`):** Requires `Skip((page - 1) * size).Take(size)` and `CountAsync()`. On large tables (>1,000,000 rows), the database must scan and discard thousands of rows for deep pages, causing high I/O latency. However, back-office admin users require random page jumps (e.g., "Go to page 47") and total record counts.
  - **Keyset / Cursor (`CursorPagedList`):** Uses an indexed column filter (`WHERE CreatedAt < @cursor ORDER BY CreatedAt DESC LIMIT @size + 1`). This is an index seek running in constant $O(1)$ time regardless of table size, and it completely avoids the expensive `COUNT(*)` query. It is immune to data drift when new sales are inserted concurrently. It is the optimal strategy for cashier transaction streams and infinite-scroll interfaces.

---

## 2. Technical Mechanics (How?)

### Q6: How does `ValidationPipelineBehavior` dynamically build `Result` or `Result<T>` when `TResponse` is generic?
* **Answer:** MediatR behaviors are registered as open generics `IPipelineBehavior<TRequest, TResponse>`. At runtime:
  1. If `TResponse` is non-generic `Result`, it returns `(TResponse)(object)Result.Failure(validationError)`.
  2. If `TResponse` is `Result<TValue>`, it checks `resultType.GetGenericTypeDefinition() == typeof(Result<>)`, retrieves the inner `TValue` argument, locates the static `Result.Failure<TValue>(Error)` generic method using reflection (`BindingFlags.Public | BindingFlags.Static`), specializes it via `.MakeGenericMethod(valueType)`, and invokes it.
  3. If `TResponse` does not inherit from `Result`, it throws an `InvalidOperationException`, enforcing architectural compliance.

---

### Q7: How does `ModelBuilderExtensions.ApplySoftDeleteQueryFilter` configure Global Query Filters without hardcoding entity types?
* **Answer:** It uses C# Expression Trees:
  1. It loops over `modelBuilder.Model.GetEntityTypes()`.
  2. For every CLR type implementing `ISoftDeletable`, it builds a parameter expression `e` of that type.
  3. It creates a property access expression `e.IsDeleted` and compares it to constant `false`: `Expression.Equal(property, Expression.Constant(false))`.
  4. It constructs a lambda `Expression.Lambda(..., parameter)` and applies it via `modelBuilder.Entity(clrType).HasQueryFilter(filter)`.
  This guarantees that EF Core appends `AND e.is_deleted = false` to every generated SQL `SELECT` query automatically.

---

### Q8: How does `AuditSaveChangesInterceptor` protect creation metadata from modification during entity updates?
* **Answer:** In EF Core, when an entity is in the `EntityState.Modified` state, the interceptor explicitly marks the creation properties as untouched:
  ```csharp
  entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
  entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
  ```
  This tells the EF Core state manager to omit `created_at` and `created_by` columns from the generated SQL `UPDATE` statement, preventing accidental or malicious overwrite of original audit records.

---

## 3. Failure & Edge Cases (What If?)

### Q9: What happens if an unhandled exception is thrown inside a MediatR handler?
* **Answer:**
  1. `PerformancePipelineBehavior` catches the execution flow in its `finally` block, stops the stopwatch, records the duration histogram, and increments `pos_requests_total`.
  2. `LoggingPipelineBehavior` catches the exception in its `catch` block, logs `LogError` with the request name and full stack trace, and re-throws with `throw;`.
  3. ASP.NET Core's pipeline routes the exception to `GlobalExceptionHandler` (`IExceptionHandler`).
  4. `GlobalExceptionHandler` captures `Activity.Current?.Id ?? HttpContext.TraceIdentifier`, logs the error with trace context, sets HTTP status `500`, and writes an RFC 7807 `ProblemDetails` JSON response containing the `traceId` while keeping internal server details private.

---

### Q10: What happens if a Domain Event handler fails during `SaveChangesAsync`?
* **Answer:** Because `DispatchDomainEventsInterceptor` executes domain events during `SavingChangesAsync` (prior to database commit):
  - In-process handlers execute within the ambient database transaction scope.
  - If a domain event handler throws an exception, `SaveChangesAsync` aborts, EF Core does not commit the transaction, and all database mutations are rolled back.
  - This ensures strong consistency between aggregate state and local event side-effects. (For eventual consistency across services, integration events are deferred to a separate Outbox publisher).

---

## 4. Architectural Trade-Offs & Senior Reflections

| Architectural Choice | What did we optimize for? | What complexity or limitation did we accept? |
| :--- | :--- | :--- |
| **In-Process Domain Events via MediatR** | Simplicity, immediate in-process consistency, pure domain models without bus dependencies. | Handlers execute synchronously before DB commit; long-running event handlers increase database transaction lock time. |
| **Monolithic Shared Assembly (`BuildingBlocks.dll`)** | Rapid development, zero internal project reference friction across team members. | Service Domain projects theoretically have visibility into Infrastructure classes if architectural discipline lapses. |
| **Direct EF Core Dependency in `PagedList`** | Eliminates custom async query provider abstractions; high developer productivity. | Application layer holds a compile-time reference to `Microsoft.EntityFrameworkCore`. |
