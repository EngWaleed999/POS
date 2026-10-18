// Handles staff account reactivation across local persistence and Keycloak.

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.ActivateStaff;

public sealed class ActivateStaffCommandHandler : ICommandHandler<ActivateStaffCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateStaffCommandHandler(
        IUserRepository userRepository,
        IIdentityProviderService identityProviderService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _identityProviderService = identityProviderService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ActivateStaffCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // Enable in Keycloak
        var idpResult = await _identityProviderService.SetUserActiveStatusAsync(user.KeycloakUserId, true, cancellationToken);
        if (idpResult.IsFailure)
        {
            return idpResult;
        }

        var activateResult = user.Activate();
        if (activateResult.IsFailure)
        {
            return activateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
