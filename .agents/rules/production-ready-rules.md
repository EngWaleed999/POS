# POS System — Production-Ready & Experiential Failure Engineering Mandate

These rules are PERMANENT and NON-NEGOTIABLE across the entire lifecycle of the POS Platform.
Every component, layer, service, configuration, and test must follow this protocol without exception.

---

## 1. Zero Hardcoded Secrets & Credential Isolation (MANDATORY)
- NEVER commit plaintext passwords, API keys, tokens, or raw connection strings to Git, C# source code, or Docker Compose files.
- ALWAYS isolate secrets in local `.env` files protected by `.gitignore`.
- ALWAYS provide clean `.env.example` templates with empty/placeholder values for team onboarding.
- Local developer overrides must use .NET Secret Manager (`dotnet user-secrets`) or environment variables.

---

## 2. No "Toy Code" or Shortcuts
- Never write "temporary" implementations, sloppy placeholders, or naive mocks intended to be "fixed later".
- Every class, method, and configuration must adhere to SOLID, Clean Architecture, and Clean Code principles from day one.
- Code must be written to pass senior architectural review on the first attempt.

---

## 3. Strict Database & Data Integrity (Last Line of Defense)
- The database is the ultimate authority on data integrity; never rely solely on application-level checks.
- Enforce strict Foreign Keys, Non-Null constraints, and Unique Constraints at the schema level.
- When Soft Delete is enabled, ALWAYS use PostgreSQL Filtered / Partial Indexes (`WHERE is_deleted = false`) to prevent unique collision bugs on deleted records.
- All foreign keys must have explicit indexes to eliminate sequential table scans on JOINs.

---

## 4. Experiential Failure Engineering & Concurrency Protocol (MANDATORY)
- **NO Premature Auto-Fixes:** NEVER silently patch or auto-solve advanced failure modes (such as Concurrency Race Conditions, Distributed State Drift, Transient Database Drops, or Idempotency Violations) during initial feature coding. The developer MUST experience the failure first-hand.
- **The 4-Stage Learning & Hardening Cycle:**
  1. **Stage 1 (Pure Domain Implementation):** Implement the business feature cleanly according to domain requirements and invariants.
  2. **Stage 2 (Failure Simulation & Stress Testing):** After building the feature, design deliberate stress tests (Edge Cases, Boundaries, Concurrent Race Conditions via `Task.WhenAll`, simulated network/DB timeouts).
  3. **Stage 3 (Witness & Socratic Analysis):** Observe the actual failure, exception, or data corruption together in real-time. Discuss:
     - What problem occurred?
     - What are the realistic architectural solutions (e.g. Optimistic vs Pessimistic Locking, Polly Retries, Idempotent Queuing, Distributed Locks)?
     - What are the trade-offs (latency, throughput, complexity, database locks)?
  4. **Stage 4 (Deliberate Hardening):** The developer chooses the best architectural approach based on the trade-offs, and only then is the production-grade fix implemented, verified, and documented.

---

## 5. Performance & Resource Discipline
- Query operations (Reads) must disable EF Core change tracking (`AsNoTracking`) by default to conserve memory and CPU allocations.
- Never fetch entire tables into memory; use server-side projection (`Select`), pagination, and indexed filtering.
- Keep domain aggregate boundaries small and tightly focused; avoid pulling large relational graphs into memory.

---

## 6. Observability, Security & Clean API Surface
- API errors must strictly comply with RFC 7807 / RFC 9457 `ProblemDetails` via Result pattern mapping.
- Never leak internal infrastructure exceptions, stack traces, or SQL error details to clients.
- Use structured logging (`Serilog`) with correlation IDs and audit timestamps on all state mutations.
