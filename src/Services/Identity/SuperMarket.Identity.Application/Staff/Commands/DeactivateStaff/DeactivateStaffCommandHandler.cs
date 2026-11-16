// -------------------------------------------------------------------------
// Business Logic Flow:
// 1. [Pre-flight Validation]: Verify staff member existence in repository.
// 2. [External IDP Deactivation]: Disable user account in Keycloak to block refresh tokens.
// 3. [External IDP Session Invalidation]: Terminate all active sessions in Keycloak immediately.
// 4. [Domain Execution]: Mutate staff aggregate to deactivated state (raises UserDeactivatedDomainEvent).
// 5. [Persistence]: Commit domain state change into PostgreSQL database.
// 6. [Cache Invalidation & Blacklisting]: Evict permission sets and write disabled tombstone in Redis.
// -------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Authorization;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.DeactivateStaff;

public sealed class DeactivateStaffCommandHandler : ICommandHandler<DeactivateStaffCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly IPermissionService _permissionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateStaffCommandHandler> _logger;

    public DeactivateStaffCommandHandler(
        IUserRepository userRepository,
        IIdentityProviderService identityProviderService,
        IPermissionService permissionService,
        IUnitOfWork unitOfWork,
        ILogger<DeactivateStaffCommandHandler> logger)
    {
        _userRepository = userRepository;
        _identityProviderService = identityProviderService;
        _permissionService = permissionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(DeactivateStaffCommand command, CancellationToken cancellationToken)
    {
        // --- 1. Pre-flight Validation: Verify staff user existence ---
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // --- 2. External IDP Deactivation: Disable user account in Keycloak ---
        var idpResult = await _identityProviderService.SetUserActiveStatusAsync(user.KeycloakUserId, false, cancellationToken);
        if (idpResult.IsFailure)
        {
            return idpResult;
        }

        // --- 3. External IDP Session Invalidation: Terminate active sessions in Keycloak ---
        try
        {
            var logoutResult = await _identityProviderService.LogoutUserAsync(user.KeycloakUserId, cancellationToken);
            if (logoutResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to invalidate Keycloak sessions for deactivated user {UserId} ({KeycloakUserId}): {Error}. Deactivation will proceed.",
                    user.Id,
                    user.KeycloakUserId,
                    logoutResult.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Unexpected error while invalidating Keycloak sessions for deactivated user {UserId} ({KeycloakUserId}). Deactivation will proceed.",
                user.Id,
                user.KeycloakUserId);
        }

        // --- 4. Domain Execution: Mutate staff aggregate to deactivated state ---
        var deactivateResult = user.Deactivate();
        if (deactivateResult.IsFailure)
        {
            return deactivateResult;
        }

        // --- 5. Persistence: Commit domain state change into PostgreSQL database ---
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // --- 6. Cache Invalidation & Blacklisting: Evict permissions and write disabled tombstone ---
        await _permissionService.RevokeUserAccessAsync(user.Id, cancellationToken);

        return Result.Success();
    }
}
