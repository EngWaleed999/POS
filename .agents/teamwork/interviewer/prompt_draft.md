This is a single self-contained fix; keep it small and focused. Add a call to Keycloak's `POST admin/realms/{realm}/users/{id}/logout` REST endpoint to terminate all active sessions for a staff member immediately upon role change in the SuperMarket POS Identity Service, forcing an immediate re-authentication with updated roles and permissions.

Integrity mode: development

## Requirements

### R1. Keycloak User Logout Integration
Add `Task<Result> LogoutUserAsync(string keycloakUserId, CancellationToken cancellationToken = default);` to `IIdentityProviderService` and implement it in `KeycloakIdentityProviderService`. The implementation must issue an authenticated `POST` request to `admin/realms/{realm}/users/{keycloakUserId}/logout` using the configured Keycloak HTTP client and admin credentials.

### R2. Invalidate Active Sessions in Role Change Workflow
In `ChangeStaffRoleCommandHandler`, call `_identityProviderService.LogoutUserAsync(user.KeycloakUserId, cancellationToken)` after `ReplaceRoleAsync`. In accordance with resilience and domain integrity patterns: role replacement in Keycloak remains the core invariant; if `LogoutUserAsync` fails due to transient network or Keycloak issues, log a warning with structured logging and proceed with updating the staff role in persistence to maintain eventual consistency without rolling back the role change.

### R3. Unit Testing and Verification
Update and add unit tests in `ChangeStaffRoleCommandHandlerTests.cs` verifying that `LogoutUserAsync` is invoked when a role changes (both successful logout and resilient failure logging). Follow Clean Architecture, zero-hardcoded secrets, defensive programming, and structured logging.

## Verification Resources
Run unit tests for the Identity Application project:
- `dotnet test --no-restore tests/Services/Identity/SuperMarket.Identity.Application.UnitTests/`
Run the full test suite to ensure no regressions across the solution:
- `dotnet test`

## Acceptance Criteria

### Interface and Implementation
- [ ] `IIdentityProviderService` declares `Task<Result> LogoutUserAsync(string keycloakUserId, CancellationToken cancellationToken = default);`.
- [ ] `KeycloakIdentityProviderService.LogoutUserAsync` sends an authenticated `POST` request to `admin/realms/{realm}/users/{keycloakUserId}/logout`.
- [ ] `KeycloakIdentityProviderService.LogoutUserAsync` correctly handles successful responses (204 No Content / 200 OK) and non-success HTTP status codes with structured error reporting.

### Handler Integration
- [ ] `ChangeStaffRoleCommandHandler` calls `_identityProviderService.LogoutUserAsync` with `user.KeycloakUserId` after `ReplaceRoleAsync` succeeds.
- [ ] If `LogoutUserAsync` fails, a warning is logged and the role change continues to save changes successfully.

### Testing
- [ ] Unit tests in `tests/Services/Identity/SuperMarket.Identity.Application.UnitTests/Staff/Commands/ChangeStaffRoleCommandHandlerTests.cs` cover `LogoutUserAsync` invocation during role change.
- [ ] `dotnet test --no-restore tests/Services/Identity/SuperMarket.Identity.Application.UnitTests/` succeeds with all tests passing.
- [ ] `dotnet test` succeeds across all solution test projects with zero test failures.

