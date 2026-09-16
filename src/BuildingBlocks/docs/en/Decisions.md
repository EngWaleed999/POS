# SuperMarket.BuildingBlocks — Architecture Decision Records (ADRs)

> **Scope:** Formal documentation of key architectural and design decisions, alternatives evaluated, trade-offs, and implementation evidence.

---

## Decision 1: Result Pattern & ROP vs. Throwing Exceptions for Domain Failures

* **Context:** In a high-throughput enterprise POS system, operations frequently fail due to expected business conditions (e.g. barcode not found, insufficient cash in drawer, register shift already closed). Using `throw new BusinessException(...)` causes significant CPU and memory overhead from stack trace generation and hides failure paths from the type system.
* **Decision:** Adopt an explicit, immutable `Result` and `Result<TValue>` pattern paired with Railway-Oriented Programming extension methods (`Match`, `Ensure`, `Map`, `Bind`).
* **Reason:** Eliminates control-flow exceptions, makes failure branches visible in method signatures, and provides allocation-efficient error handling.
* **Alternatives:**
  1. *Throwing Custom Exceptions:* Rejected due to severe performance degradation under high load and lack of compile-time enforcement.
  2. *Using FluentResults / OneOf Third-Party Libraries:* Rejected to keep the core foundation free from external dependencies and tailored specifically to the POS domain.
* **Trade-offs:**
  - *Gained:* Zero stack-trace overhead, explicit compiler-checked error propagation, uniform RFC 7807 conversion.
  - *Sacrificed:* Requires developers to write explicit result checks or functional chains rather than letting exceptions bubble up automatically.
* **Current Status:** `Implemented`
* **Evidence:** `Results/Result.cs`, `Results/ResultT.cs`, `Results/ResultExtensions.cs`, `Results/Error.cs`

---

## Decision 2: Capability Interfaces vs. Monolithic BaseEntity

* **Context:** Different database entities require different behavioral capabilities. For example, a `Sale` requires audit timestamps, user attribution, and soft-delete; an `AuditLog` requires creation timestamps but must never be updated or soft-deleted; a `BranchSchedule` may only require an `IsActive` flag.
* **Decision:** Implement granular capability interfaces (`IAuditableEntity`, `ISoftDeletable`, `IActivatable`) rather than forcing a single bloated `BaseEntity` on all domain models.
* **Reason:** Adheres to the Interface Segregation Principle (ISP) and Composition over Inheritance. Prevents database tables from carrying redundant or logically conflicting columns.
* **Alternatives:**
  1. *Single Monolithic BaseEntity:* Rejected because it violates ISP and produces bloated database tables with null columns.
* **Trade-offs:**
  - *Gained:* Clean, normalized tables; granular opt-in behaviors; reusable infrastructure interceptors.
  - *Sacrificed:* Entities must implement multiple interfaces and repeat property declarations (mitigated by clean code conventions).
* **Current Status:** `Implemented`
* **Evidence:** `Domain/IAuditableEntity.cs`, `Domain/ISoftDeletable.cs`, `Domain/IActivatable.cs`

---

## Decision 3: Automated Auditing & Soft Delete via SaveChangesInterceptor

* **Context:** In enterprise systems, developers frequently forget to manually populate `UpdatedAt`, `CreatedBy`, or accidentally execute hard SQL `DELETE` commands, resulting in data corruption or audit gaps.
* **Decision:** Implement `AuditSaveChangesInterceptor` hooking into EF Core's `SavingChanges` and `SavingChangesAsync` pipeline.
* **Reason:** Centralizes compliance logic, automates UTC stamping and user extraction from `ICurrentUserContext`, converts hard deletes to soft updates, and enforces immutability on `CreatedAt`/`CreatedBy`.
* **Alternatives:**
  1. *Overriding SaveChanges inside a BaseDbContext:* Rejected because interceptors are more composable, testable, and follow modern EF Core best practices.
  2. *Handling Auditing in Handlers or Repositories:* Rejected due to human error and massive code duplication.
* **Trade-offs:**
  - *Gained:* 100% guarantee that every database persistence operation complies with audit and retention policies.
  - *Sacrificed:* Small reflection/ChangeTracker inspection overhead per `SaveChanges` call.
* **Current Status:** `Implemented`
* **Evidence:** `Infrastructure/AuditSaveChangesInterceptor.cs`

---

## Decision 4: In-Process Domain Events Dispatching via EF Core Interceptor

* **Context:** Domain entities raise events when state changes occur (e.g. `ShiftClosedDomainEvent`). These events must be dispatched reliably when the aggregate is saved.
* **Decision:** Capture events in `AggregateRoot<TId>` and dispatch them using `DispatchDomainEventsInterceptor` before the database transaction completes.
* **Reason:** Enforces aggregate encapsulation. Prevents domain entities from holding dependencies on MediatR or service providers, while ensuring events are dispatched automatically without manual handler calls.
* **Alternatives:**
  1. *Manual Dispatching in Command Handlers:* Rejected because developers might forget to publish events after calling `SaveChangesAsync()`.
  2. *Injecting IPublisher into Entities:* Rejected because it violates DDD purity and pollutes the domain model with infrastructure concerns.
* **Trade-offs:**
  - *Gained:* Pure domain entities, automated dispatching, idempotency via clearing events before publishing.
  - *Sacrificed:* Events are executed in-process within the same transaction; failure in an event handler rolls back the database save.
* **Current Status:** `Implemented`
* **Evidence:** `Infrastructure/DispatchDomainEventsInterceptor.cs`, `Domain/AggregateRoot.cs`

---

## Decision 5: Separation of Logging and Performance Pipeline Behaviors

* **Context:** Logging lifecycle events and measuring performance metrics are distinct cross-cutting concerns.
* **Decision:** Create two separate MediatR pipeline behaviors: `LoggingPipelineBehavior` and `PerformancePipelineBehavior`.
* **Reason:** Adheres to the Single Responsibility Principle (SRP). Allows performance thresholds and metrics collection to be tested, configured, or altered independently of textual logging.
* **Alternatives:**
  1. *Combined All-in-One Pipeline:* Rejected because it creates a God pipeline behavior mixing logging levels with metrics collection and stopwatch timers.
* **Trade-offs:**
  - *Gained:* High cohesion, independent configuration via `PerformanceSettings`, clean codebase.
  - *Sacrificed:* One additional delegate invocation in the MediatR pipeline (sub-microsecond overhead).
* **Current Status:** `Implemented`
* **Evidence:** `Application/LoggingPipelineBehavior.cs`, `Application/PerformancePipelineBehavior.cs`

---

## Decision 6: Native System.Diagnostics.Metrics vs. External Telemetry SDKs

* **Context:** The application needs to publish RED metrics (Rate, Errors, Duration) for Prometheus and Grafana dashboards.
* **Decision:** Use .NET's built-in `System.Diagnostics.Metrics.Meter`, `Counter<T>`, and `Histogram<T>` directly inside `PerformancePipelineBehavior`.
* **Reason:** Provides native, high-performance telemetry with zero external NuGet dependencies. Consuming Web API projects can bind any OpenTelemetry exporter (OTLP, Prometheus, Console) without changing `BuildingBlocks`.
* **Alternatives:**
  1. *Referencing OpenTelemetry SDK in BuildingBlocks:* Rejected to avoid binding the shared core to specific telemetry SDK packages and versions.
* **Trade-offs:**
  - *Gained:* Zero dependencies, optimal performance, standard .NET diagnostics support.
  - *Sacrificed:* Exporters must be configured in the host Web API startup.
* **Current Status:** `Implemented`
* **Evidence:** `Application/PerformancePipelineBehavior.cs`

---

## Decision 7: Dual-Strategy Pagination (Offset vs. Keyset)

* **Context:** Administrative back-office users need to jump to specific pages and see total item counts, whereas cashier checkout terminals query millions of transactions continuously and cannot tolerate database lock contention or slow `COUNT(*)` queries.
* **Decision:** Implement both Offset Pagination (`PagedList<T>`, `PaginationParams`) and Keyset Pagination (`CursorPagedList<T, TCursor>`, `CursorParams<TCursor>`).
* **Reason:** Provides the right performance characteristics for each specific workload rather than forcing a one-size-fits-all compromise.
* **Alternatives:**
  1. *Offset Pagination Only:* Rejected because `Skip(100000).Take(20)` causes devastating database latency on large POS transaction tables.
  2. *Keyset Pagination Only:* Rejected because back-office administrative grids require total record counts and page jump controls.
* **Trade-offs:**
  - *Gained:* $O(1)$ constant-time queries for high-volume cashier flows; rich UI navigation for management grids.
  - *Sacrificed:* Developers must choose the appropriate pagination strategy per use case.
* **Current Status:** `Implemented`
* **Evidence:** `Application/PagedList.cs`, `Application/CursorPagedList.cs`, `Application/PaginationParams.cs`, `Application/CursorParams.cs`
