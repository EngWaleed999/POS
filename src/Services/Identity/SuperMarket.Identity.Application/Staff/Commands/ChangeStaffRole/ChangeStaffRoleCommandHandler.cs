// Handles changing a staff member's role and synchronizing permissions with Keycloak.

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Commands.ChangeStaffRole;

public sealed class ChangeStaffRoleCommandHandler : ICommandHandler<ChangeStaffRoleCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeStaffRoleCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IIdentityProviderService identityProviderService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _identityProviderService = identityProviderService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ChangeStaffRoleCommand command, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        var newRole = await _roleRepository.GetByIdAsync(command.NewRoleId, cancellationToken);
        if (newRole is null)
        {
            return Result.Failure(UserErrors.RoleNotFound);
        }

        // Synchronize role assignment in external Identity Provider
        var idpResult = await _identityProviderService.AssignRoleAsync(user.KeycloakUserId, newRole.RoleName, cancellationToken);
        if (idpResult.IsFailure)
        {
            return idpResult;
        }

        var changeResult = user.ChangeRole(command.NewRoleId);
        if (changeResult.IsFailure)
        {
            return changeResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
