# Clean Code, SOLID & Pragmatic Architecture Principles

## 1. Core Engineering Philosophy
- Always write explicit, self-documenting code over clever, implicit abstractions.
- Optimize for high readability, low cognitive load, and effortless future maintenance.
- Any engineer reading the code must understand the business intent within seconds.

## 2. SOLID Enforcement
- **Single Responsibility (SRP):** Split capabilities into focused units. Avoid multi-purpose god classes.
- **Open/Closed (OCP):** New behaviors and services must be extensible through composition, events, and interfaces without modifying existing core logic.
- **Liskov Substitution (LSP):** Inherited contracts must never alter the expected behavior of base abstractions.
- **Interface Segregation (ISP):** Prefer small, capability-driven interfaces (`IAuditableEntity`, `ISoftDeletable`, `IActivatable`) over monolithic base classes.
- **Dependency Inversion (DIP):** High-level application/domain logic must never depend on low-level infrastructure details.

## 3. Pragmatic Domain-Driven Design (DDD)
- Apply rich domain models and Aggregate Roots to core contexts with complex invariants (Sales, Shifts, Inventory, Branch Security).
- Keep simple lookups and append-only ledgers lightweight without speculative domain complexity.
- Separate transient domain errors from unrecoverable infrastructure failures using the Result Pattern.
