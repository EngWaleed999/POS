// Handles staff deactivation across local persistence and the external Identity Provider.

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.DeactivateStaff;

public sealed class DeactivateStaffCommandHandler : ICommandHandler<DeactivateStaffCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateStaffCommandHandler(
        IUserRepository userRepository,
        IIdentityProviderService identityProviderService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _identityProviderService = identityProviderService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateStaffCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // Disable in Keycloak to immediately block token refreshes
        var idpResult = await _identityProviderService.SetUserActiveStatusAsync(user.KeycloakUserId, false, cancellationToken);
        if (idpResult.IsFailure)
        {
            return idpResult;
        }

        var deactivateResult = user.Deactivate();
        if (deactivateResult.IsFailure)
        {
            return deactivateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
