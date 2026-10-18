// Port contract for interacting with external Identity and Access Management (Keycloak).

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Application.Abstractions.Authentication;

public interface IIdentityProviderService
{
    Task<Result<string>> CreateUserAsync(
        string username,
        string? email,
        string phoneNumber,
        string fullName,
        string initialPassword,
        CancellationToken cancellationToken = default);

    Task<Result> AssignRoleAsync(
        string keycloakUserId,
        string roleName,
        CancellationToken cancellationToken = default);

    Task<Result> SetUserActiveStatusAsync(
        string keycloakUserId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteUserAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GenerateUserTokenAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default);
}
