# POS System — Architectural Rules & Constraints

These rules are NON-NEGOTIABLE. Follow them in every response without exception.

---

## 1. General Code Style

- NO XML `<summary>` comments anywhere in C# files.
- Use a single one-line header comment at the top of every file.
- Use `// ---` section dividers inside classes instead of regions.
- All code, identifiers, and configuration must be in English.
- All architecture discussion must be in Arabic with technical terms kept in English.

---

## 2. Architecture — Clean Architecture + DDD

- Domain layer has zero dependencies on external libraries (no MediatR, no FluentValidation, no EF Core).
- Application layer depends on Domain only. Uses MediatR + FluentValidation.
- Infrastructure depends on Application + Domain. Uses EF Core, MassTransit, Keycloak.
- API depends on Application only. Uses ASP.NET Core, Scalar, ProblemDetails.
- Apply Pragmatic DDD — Rich Domain Models for Core Domains (Identity, Sales, Inventory) only.
- Domain Events are stored in AggregateRoot._domainEvents list. Entities do NOT call MediatR or RabbitMQ directly.
- Domain Events are dispatched by DispatchDomainEventsInterceptor in Infrastructure after SaveChangesAsync.
- Integration Events are published via MassTransit Transactional Outbox only.

---

## 3. Entity Design Rules

- All entity properties must have private set — NEVER public set.
- ISoftDeletable, IAuditableEntity, IActivatable must use Explicit Interface Implementation.
- Constructors: one private() for EF Core, one private(...) full constructor.
- Factory Method: always public static Result<T> Create(...).
- Phone numbers are ALWAYS string — never int or long. Stored as VARCHAR(20) in PostgreSQL.
- Timestamps are ALWAYS DateTimeOffset — never DateTime. UTC only.

---

## 4. Validation — Two-Tier Strategy (MANDATORY)

- Application Layer (FluentValidation): handles ALL format/structural validation (email RFC, phone E.164, length, Regex).
- Domain Layer (Guard Clauses): handles ONLY defensive business invariants (null/empty checks, Guid.Empty, state checks). NO Regex. NO Contains('@') style checks.
- ValidationPipelineBehavior MUST return Result.Failure(ValidationError) — never throw exceptions for control flow. It groups failures into an IReadOnlyDictionary<string, string[]> by property name so frontends get field-level errors.
- ResultProblemDetailsExtensions maps ValidationError to RFC 7807 / RFC 9457 ValidationProblemDetails (HTTP 400 Bad Request) containing the field-level errors dictionary.

---

## 5. BuildingBlocks Constraints

- Any change to BuildingBlocks must be backward compatible — it is shared by ALL services.
- Result<T> and Result are the only return types for Command Handlers and Domain operations.
- Error record is immutable. Specialized ValidationError carries structured field-level errors dictionary.
- ValidationPipelineBehavior must return Result.Failure(ValidationError) without throwing exceptions.

---

## 6. Identity Service Specific Rules

- Identity Service handles Authentication only: PIN verification, Keycloak token issuance, account lockout, device binding.
- Shift scheduling belongs to Sales Service — never enforce shift time boundaries in Identity.
- Soft Shift Scheduling: no hard block at shift start/end — shifts open and close manually.
- Account lockout: 3 failed attempts triggers 15-minute lockout. Emit UserLockedOutDomainEvent with UTC timestamp for CCTV synchronization.
- PIN stored as Argon2id hash only — never plaintext. Field: PinHash.
- BranchId is nullable on User — user may not yet be assigned to a branch.

---

## 7. Database & EF Core Rules

- PostgreSQL only. Use snake_case for all table and column names.
- permission_group_items uses Composite Primary Key (group_id, permission_id) — no surrogate key.
- role_permission_groups uses Composite Primary Key (role_id, group_id) — no surrogate key.
- permissions table: UNIQUE constraint on (resource, action, scope).
- branch_operating_hours table: UNIQUE constraint on (branch_id, day_of_week).
- users.phone_number: VARCHAR(20) NOT NULL UNIQUE.
- users.email: VARCHAR(100) NULL.
- AuditSaveChangesInterceptor auto-fills CreatedAt, UpdatedAt, DeletedAt — never set manually in Handlers.
- Soft delete via ISoftDeletable Explicit Interface + EF Core Global Query Filters (IsDeleted == false).

---

## 8. Naming Conventions

- Commands: CreateUserCommand, UpdateUserCommand.
- Handlers: CreateUserCommandHandler.
- Validators: CreateUserCommandValidator.
- Domain Events: UserCreatedDomainEvent, UserLockedOutDomainEvent.
- Integration Events: UserCreatedIntegrationEvent.
- Errors: UserErrors.EmptyUsername, UserErrors.AccountLockedOut.
- Feature folders: Features/Users/Commands/CreateUser/.

---

## 9. What NOT to do

- NEVER add Contains('@') or naive email checks inside Domain entities.
- NEVER add speculative fields not in docs/SYSTEM_SPECIFICATION_AND_ROLES.md.
- NEVER inject MediatR, IPublisher, or broker interfaces into Domain entities.
- NEVER use public set on entity properties.
- NEVER use DateTime — always DateTimeOffset.
- NEVER throw exceptions for expected validation flow control — always return Result.Failure(ValidationError).
- NEVER use int or long for phone numbers, postal codes, ID card numbers, or bank account numbers.
- NEVER touch Sales, Inventory, or Catalog services during Identity Sprint work.

---

## 10. Current Sprint Context

- Sprint 1: Identity Service Domain Layer and Database.
- Completed: User Aggregate Root, Branch, BranchOperatingHours, POSRegister, Address, BranchCode, RegisterCode, Domain Events, Domain Errors.
- In Progress: Domain Unit Testing (`SuperMarket.Identity.Domain.UnitTests`).
- Next: Infrastructure Layer (`IdentityDbContext`, EF Core Configurations, Initial Migration).

---

## 11. Automated Testing Standards & Engineering Discipline (MANDATORY)

- **Pre-Test Comprehensive Plan:** Before writing any test for any layer or component, you MUST present a structured, comprehensive Test Plan covering Scope, Invariants, Happy Paths, Failure Invariants, Edge Cases, and Test Level for user alignment.
- **Data-Driven Testing by Default (`[Theory]` + `[InlineData]`):** Whenever testing multiple inputs, equivalence partitions, boundary values (min/max), or similar validation errors for the same method/contract, you MUST use `[Theory]` with `[InlineData]` rows.
- **FORBIDDEN:** NEVER generate repetitive single-case `[Fact]` methods that share the same Arrange-Act-Assert logic with trivial input differences.
- **Edge Cases & Boundary Value Analysis (BVA):** Explicitly test boundary values (minimum length, maximum length, one above, one below), midnight time transitions, empty/null variants, and illegal state transitions.
- **Inspect Definitions First:** ALWAYS inspect the entity and domain event definitions to verify exact property names BEFORE writing assertions, preventing CS1061 compile errors and eliminating wasted token correction cycles.
- **Restricted Use of `[Fact]`:** Restrict `[Fact]` ONLY to unique execution paths (e.g., verifying Domain Event emission payload, or complex multi-step state machine workflows).
- **Documentation Synchronization:** After completing tests or any architectural milestone, you MUST update project documentation in `docs/` (e.g. `docs/SPRINTS_AND_TASKS.md`) and maintain a dedicated test analysis document detailing all tested scenarios and boundaries to keep documentation 100% in sync with code.

