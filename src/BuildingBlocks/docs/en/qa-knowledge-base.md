# SuperMarket.BuildingBlocks — Engineering Knowledge Base & Technical Interview Guide

> **Scope:** Advanced engineering reference covering architectural philosophy, critical edge cases, code mechanics, trade-off analysis, and technical interview questions extracted directly from `SuperMarket.BuildingBlocks` and its test suite.

---

## 1. Foundational Architecture Questions (Why?)

### Q1: Why did we adopt the Result Pattern instead of throwing Exceptions for business and validation failures?
* **Answer:** In high-throughput Point of Sale (POS) environments processing hundreds of transactions and barcode scans per second, relying on exceptions for control flow is an architectural anti-pattern:
  1. **High Runtime Overhead:** Throwing exceptions forces the CLR to capture stack frames, walk unwinding tables, and allocate heap metadata, triggering unnecessary CPU spikes and Garbage Collector pressure.
  2. **Implicit and Unclear API Contracts:** A method signature like `Task<Order> CheckoutAsync(...)` conceals potential business failure modes. In contrast, `Task<Result<Order>>` uses the C# type system to force calling code to explicitly handle both success and failure states at compile time.
  3. **Strict Separation of Concerns:** Exceptions are reserved exclusively for catastrophic, unexpected infrastructure events (e.g., database connection drops, hardware faults). These are caught globally by `GlobalExceptionHandler` and mapped to HTTP 500. Predictable domain/validation failures return `Result.Failure` and map to standard 4xx ProblemDetails.

---

### Q2: Why use atomic capability interfaces (`IAuditableEntity`, `ISoftDeletable`) instead of a monolithic `BaseEntity`?
* **Answer:** Applying **Composition over Inheritance** and the **Interface Segregation Principle (ISP)** prevents the emergence of God Objects:
  - Forcing auditing and soft-deletion fields in a base class would burden entities that should never be deleted or updated (e.g., immutable `AuditLog` records or daily cash register Z-reports) with wasteful, unused database columns (`DeletedAt`, `DeletedBy`, `UpdatedAt`).
  - With capability interfaces, each entity selectively implements only what it requires. `AuditSaveChangesInterceptor` dynamically inspects these interfaces at runtime to apply rules automatically.

---

### Q3: Why does `DispatchDomainEventsInterceptor` clear domain events before publishing them?
* **Answer:** To enforce **Idempotency and Infinite Loop Protection**:
  - If events were cleared after publishing, and an event handler triggered a secondary `SaveChangesAsync()` within the same transaction scope, the interceptor would re-read and re-dispatch the uncleared events, causing duplicate side effects or an infinite recursion loop.
  - Clearing the queue (`root.ClearDomainEvents()`) prior to dispatching guarantees that each event instance is published exactly once.

---

### Q4: Why inject `TimeProvider` instead of directly calling `DateTime.UtcNow` in the save interceptor?
* **Answer:** Direct calls to `DateTime.UtcNow` couple business logic to the operating system clock, making deterministic unit testing impossible when validating temporal boundaries. Injecting an abstract `TimeProvider` (which defaults to `TimeProvider.System` in production) allows test suites to inject `FakeTimeProvider` and verify timestamps with zero time drift.

---

### Q5: What is the practical difference between Offset Pagination and Keyset (Cursor) Pagination?
* **Answer:**
  - **Offset Pagination (`PagedList`):** Relies on `Skip((page - 1) * size).Take(size)` and a mandatory `COUNT(*)` query. As tables scale to millions of rows, the database must scan and discard thousands of disk blocks, causing linear performance degradation $O(N)$. However, it remains necessary for administrative UI tables requiring explicit page jumping and total counts.
  - **Keyset / Cursor Pagination (`CursorPagedList`):** Queries using index seek comparisons (`WHERE CreatedAt < @cursor ORDER BY CreatedAt DESC LIMIT @size + 1`). It achieves constant $O(1)$ query execution time regardless of table volume, eliminates the expensive `COUNT(*)` scan entirely, and avoids data drift anomalies when real-time sales are appended.

---

## 2. Code Mechanics & Implementation Details (How?)

### Q6: How does `ValidationPipelineBehavior` dynamically instantiate `Result` or `Result<T>` on failure?
* **Answer:** The behavior is registered as an Open Generic (`IPipelineBehavior<TRequest, TResponse>`). When validation fails:
  1. If `TResponse` is non-generic `Result`, it returns `(TResponse)(object)Result.Failure(error)`.
  2. If `TResponse` is generic `Result<TValue>`, reflection detects the definition (`resultType.GetGenericTypeDefinition() == typeof(Result<>)`), extracts the underlying `TValue`, retrieves the static factory method `Result.Failure<TValue>(Error)`, calls `.MakeGenericMethod(valueType)`, and invokes it.
  3. If `TResponse` does not derive from `Result`, an explicit `InvalidOperationException` is thrown to guard architectural integrity.

---

### Q7: How does `ModelBuilderExtensions.ApplySoftDeleteQueryFilter` construct global query filters dynamically?
* **Answer:** It uses Runtime Expression Trees during EF Core model creation:
  1. Iterates over all entity types registered in `modelBuilder.Model.GetEntityTypes()`.
  2. Identifies entities implementing `ISoftDeletable`.
  3. Generates a `ParameterExpression` representing the entity instance (`e`).
  4. Generates a member access expression for `e.IsDeleted` and compares it to a boolean constant `false`.
  5. Compiles a lambda expression and applies it via `modelBuilder.Entity(clrType).HasQueryFilter(...)`.
  EF Core automatically appends `AND e.is_deleted = false` to every generated SQL query unless explicitly bypassed via `.IgnoreQueryFilters()`.

---

### Q8: How does `AuditSaveChangesInterceptor` protect `CreatedAt` and `CreatedBy` from tampering on entity update?
* **Answer:** When examining modified entities, the interceptor directly commands the EF Core `ChangeTracker`:
  ```csharp
  entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
  entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
  ```
  This explicitly instructs the EF Core query pipeline to exclude these columns from the generated SQL `UPDATE` statement, guaranteeing historical metadata remains immutable.

---

## 3. Edge Cases & Resilience (What If?)

### Q9: What happens if a Domain Event Handler crashes during `SaveChangesAsync`?
* **Answer:** Because `DispatchDomainEventsInterceptor` dispatches domain events in-memory during `SavingChangesAsync` (prior to committing the transaction to the database):
  - Handlers execute within the ambient database transaction scope.
  - If any handler throws an unhandled exception, `SaveChangesAsync()` aborts immediately, and the transaction is rolled back, preserving atomic transactional consistency within the bounded context.

---

### Q10: What happens when an unhandled exception occurs inside the MediatR Pipeline?
* **Answer:**
  1. `PerformancePipelineBehavior` executes its `finally` block to record latency and increment the OpenTelemetry request counter.
  2. `LoggingPipelineBehavior` catches the exception, logs an `Error` level log with full stack trace, and re-throws (`throw;`).
  3. The exception reaches `GlobalExceptionHandler` implementing ASP.NET Core's `IExceptionHandler`.
  4. The handler extracts `TraceIdentifier`, sets HTTP 500, logs the failure, and returns a sanitized RFC 7807 ProblemDetails payload containing only the correlation `traceId` to the client.

---

## 4. Architectural Trade-Off Analysis

| Architectural Decision | Optimized For | Accepted Cost / Constraint |
| :--- | :--- | :--- |
| **In-Memory MediatR Domain Events** | Simplicity, immediate in-process consistency, clean domain models. | Handlers execute synchronously before DB commit; slow handlers increase database lock duration. |
| **Single DLL for BuildingBlocks** | Rapid development, straightforward project references, unified maintenance. | Domain projects could theoretically reference Infrastructure classes if code review discipline lapses. |
| **Direct EF Core Dependency in `PagedList`** | Eliminates complex async query abstraction layers, utilizes native EF Core performance. | Application layer holds a package reference to `Microsoft.EntityFrameworkCore`. |

---

## 5. Advanced Automated Testing & QA Questions

### Q11: Why did we test EF Core interceptors using `Microsoft.EntityFrameworkCore.InMemory` instead of mocking `DbContext`?
* **Answer:** Mocking `DbContext` or `DbSet` via Moq is a well-known architectural anti-pattern:
  - EF Core interceptors fundamentally depend on the complex internal state machine of the `ChangeTracker` (`Added`, `Modified`, `Deleted`). Simulating this with mocks produces brittle, unrealistic tests.
  - An `InMemoryDatabase` provides a 100% real `DbContext` that executes interceptor hooks, runs expression tree query filters, and exercises entity states in sub-millisecond execution times without Docker or network dependencies.

---

### Q12: How do you prove that a test suite is resilient and not just full of "Tautological Tests"?
* **Answer:** Through **Mutation Testing**:
  - Tautological tests pass unconditionally because their assertions are superficial (e.g., `Assert.NotNull(result)`).
  - To prove test quality, we inject synthetic bugs (mutants) into production code (such as commenting out `entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;`).
  - A resilient test suite fails immediately ("kills the mutant") andPinpoints the exact contract violation. This was empirically proven in our `AuditSaveChangesInterceptorTests`.

---

### Q13: What is the Generic Interface Dispatch trap when verifying MediatR in Moq?
* **Answer:** When code dispatches an event via an interface variable:
  ```csharp
  IDomainEvent domainEvent = new OrderCreatedDomainEvent();
  await _publisher.Publish(domainEvent, cancellationToken);
  ```
  The C# compiler binds the invocation to `IPublisher.Publish<IDomainEvent>()` rather than the concrete type `Publish<OrderCreatedDomainEvent>()`.
  Asserting `publisherMock.Verify(p => p.Publish(It.IsAny<OrderCreatedDomainEvent>(), ...))` will fail. The assertion must explicitly verify the interface contract:
  ```csharp
  publisherMock.Verify(p => p.Publish<IDomainEvent>(It.IsAny<OrderCreatedDomainEvent>(), ...), Times.Once);
  ```
