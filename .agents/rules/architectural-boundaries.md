---
trigger: glob
---

# POS Platform — Global Architectural Boundaries & Presentation Standards

These boundaries are PERMANENT, NON-NEGOTIABLE, and strictly enforced across ALL services (Identity, Sales, Inventory, Catalog, Purchasing, Shifts) in the POS ecosystem.
Do NOT re-litigate, challenge, or alter these boundaries in new sprint discussions without an explicit, documented business driver.

---

## 1. Architectural Style & Boundaries (Clean Architecture + CQRS)

1. **Layer Dependencies (Strict Monodirectional):**
   - `Domain`: Pure business invariants, Aggregate Roots, Value Objects, Domain Events. Zero external dependencies (No EF Core, No MediatR, No FluentValidation, No ASP.NET Core).
   - `Application`: Use Cases (CQRS Commands/Queries), MediatR Handlers, FluentValidation Validators, Ports/Abstractions. Depends ONLY on Domain.
   - `Infrastructure`: Adapters, PostgreSQL EF Core DbContext, Repositories, Seeding, Outbox, Keycloak. Implements Application Abstractions.
   - `API / Presentation`: Protocol Adapter only. Translates HTTP requests to Application Commands/Queries via MediatR `ISender`.

2. **No Leakage of Business Logic into API Layer:**
   - Presentation layer (Controllers or Minimal APIs) must NOT contain if/else business branches, transaction logic, or database operations.
   - Its only job is: `Request` -> `Command/Query` -> `await sender.Send()` -> `Result.ToHttpResult()`.

---

## 2. Error Handling & The Result Pattern Protocol

1. **Two Distinct Failure Classes:**
   - **Expected Failures (Business Invariants, Not Found, Conflicts, Validation):**
     - Returned explicitly using `Result` or `Result<T>` from `SuperMarket.BuildingBlocks.Results`.
     - NEVER throw C# exceptions for expected business or validation failures.
     - Handled at the API boundary using `result.ToProblemDetails()` or standardized Result-to-HTTP mapping (RFC 7807 / RFC 9457).
   - **Unexpected Infrastructure / Fatal Errors (NullReference, DB Connection Crash, OutOfMemory):**
     - Bubbled up to ASP.NET Core built-in Problem Details / Global Error Middleware.
     - Automatically returns HTTP 500 Internal Server Error without leaking raw exceptions or stack traces to clients.

2. **Validation Strategy (Two-Tier):**
   - **Application Tier (FluentValidation via MediatR Pipeline):** Structural & format validation (phone format, email RFC, lengths, regex, required fields). Returns `Result.Failure(ValidationError)` containing field-level error dictionary (`errors`).
   - **Domain Tier (Guard Clauses inside Entities):** Internal invariant checks (e.g., negative money, state transitions, non-empty Guids).

---

## 3. Presentation Layer Standards (Minimal APIs vs Controllers)

1. **Standardized Presentation Pattern:**
   - The platform standard for all microservices is **Minimal APIs grouped via Route Groups Extension Methods** (e.g., `app.MapRoleEndpoints()`, `app.MapBranchEndpoints()`).
   - Grouping must reside in `src/Services/{ServiceName}/SuperMarket.{ServiceName}.API/Endpoints/`.
   - Each aggregate or functional vertical slice gets its own static endpoint class.
2. **Result Mapping Consistency:**
   - Success without payload: `Results.NoContent()` (204) or `Results.Ok()` (200).
   - Success with resource creation: `Results.CreatedAtRoute()` (201).
   - Failure: `result.ToProblemDetails()` producing standardized RFC 7807 responses.
3. **Reusability via Composition, Not Inheritance:**
   - Never introduce monolithic `BaseController` hierarchies to share cross-service HTTP mapping. Shared mapping logic MUST live in `SuperMarket.BuildingBlocks.Results` as pure extension methods (`ResultProblemDetailsExtensions`).

---

## 4. Code & Discussion Protocol

- **Language:**
  - All architecture, design reasoning, and mentor dialogues must be in Arabic with technical engineering terminology in English.
  - All source code, identifiers, database tables, and comments must be in English.
- **Decision Permanence:**
  - Established patterns documented in `docs/ADR/` and this rule file are treated as foundational axioms. Focus on building and hardening features according to these boundaries, not redesigning the pipeline on every turn.
