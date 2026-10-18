// Development and baseline implementation of the Keycloak Identity Provider port.

using Microsoft.Extensions.Logging;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;

namespace SuperMarket.Identity.Infrastructure.Authentication;

public sealed class DevKeycloakIdentityProviderService : IIdentityProviderService
{
    private readonly ILogger<DevKeycloakIdentityProviderService> _logger;

    public DevKeycloakIdentityProviderService(ILogger<DevKeycloakIdentityProviderService> logger)
    {
        _logger = logger;
    }

    public Task<Result<string>> CreateUserAsync(
        string username,
        string? email,
        string phoneNumber,
        string fullName,
        string initialPassword,
        CancellationToken cancellationToken = default)
    {
        var keycloakUserId = $"kc_{Guid.NewGuid():N}";

        _logger.LogInformation(
            "Keycloak Provider: Created user account. KeycloakUserId: {KeycloakUserId}, Username: {Username}, Email: {Email}, Phone: {Phone}",
            keycloakUserId,
            username,
            email,
            phoneNumber);

        return Task.FromResult(Result.Success(keycloakUserId));
    }

    public Task<Result> AssignRoleAsync(
        string keycloakUserId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Keycloak Provider: Assigned role {RoleName} to user {KeycloakUserId}",
            roleName,
            keycloakUserId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SetUserActiveStatusAsync(
        string keycloakUserId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Keycloak Provider: Set active status {IsActive} for user {KeycloakUserId}",
            isActive,
            keycloakUserId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteUserAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Keycloak Provider: Compensating rollback executed. Deleted user {KeycloakUserId}",
            keycloakUserId);

        return Task.FromResult(Result.Success());
    }
}
