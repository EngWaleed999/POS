// -------------------------------------------------------------------------
// Business Logic Flow:
// 1. [Pre-flight Validation]: Verify staff member existence in repository.
// 2. [External IDP Activation]: Enable user account in Keycloak to allow logins and token issuance.
// 3. [Domain Execution]: Mutate staff aggregate to active state.
// 4. [Persistence]: Commit domain state change into PostgreSQL database.
// 5. [Cache Unmarking]: Remove disabled tombstone from Redis to allow clean cache re-population.
// -------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Authorization;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.ActivateStaff;

public sealed class ActivateStaffCommandHandler : ICommandHandler<ActivateStaffCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly IPermissionService _permissionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ActivateStaffCommandHandler> _logger;

    public ActivateStaffCommandHandler(
        IUserRepository userRepository,
        IIdentityProviderService identityProviderService,
        IPermissionService permissionService,
        IUnitOfWork unitOfWork,
        ILogger<ActivateStaffCommandHandler> logger)
    {
        _userRepository = userRepository;
        _identityProviderService = identityProviderService;
        _permissionService = permissionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ActivateStaffCommand command, CancellationToken cancellationToken)
    {
        // --- 1. Pre-flight Validation: Verify staff user existence ---
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // --- 2. External IDP Activation: Enable account in Keycloak ---
        var idpResult = await _identityProviderService.SetUserActiveStatusAsync(user.KeycloakUserId, true, cancellationToken);
        if (idpResult.IsFailure)
        {
            return idpResult;
        }

        // --- 3. Domain Execution: Mutate staff aggregate to active state ---
        var activateResult = user.Activate();
        if (activateResult.IsFailure)
        {
            return activateResult;
        }

        // --- 4. Persistence: Commit domain state change into PostgreSQL database ---
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // --- 5. Cache Unmarking: Remove disabled tombstone from Redis ---
        await _permissionService.UnmarkUserDisabledAsync(user.Id, cancellationToken);

        _logger.LogInformation("Successfully re-activated staff member {UserId} and cleared Redis disabled tombstone.", user.Id);

        return Result.Success();
    }
}
