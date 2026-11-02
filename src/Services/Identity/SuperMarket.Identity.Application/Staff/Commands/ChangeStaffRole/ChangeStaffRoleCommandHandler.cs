using Microsoft.Extensions.Logging;
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
    private readonly ILogger<ChangeStaffRoleCommandHandler> _logger;

    public ChangeStaffRoleCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IIdentityProviderService identityProviderService,
        IUnitOfWork unitOfWork,
        ILogger<ChangeStaffRoleCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _identityProviderService = identityProviderService;
        _unitOfWork = unitOfWork;
        _logger = logger;
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

        if (user.RoleId == newRole.Id)
        {
            return Result.Success();
        }

        var oldRole = await _roleRepository.GetByIdAsync(user.RoleId, cancellationToken);
        var oldRoleName = oldRole?.RoleName;

        // Synchronize role replacement in external Identity Provider (Keycloak)
        var idpResult = await _identityProviderService.ReplaceRoleAsync(
            user.KeycloakUserId,
            oldRoleName,
            newRole.RoleName,
            cancellationToken);

        if (idpResult.IsFailure)
        {
            return idpResult;
        }

        // Invalidate active sessions in Keycloak to force immediate re-authentication.
        // Role replacement is the core invariant; logout failure logs a warning and proceeds.
        try
        {
            var logoutResult = await _identityProviderService.LogoutUserAsync(user.KeycloakUserId, cancellationToken);
            if (logoutResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to invalidate active sessions in Keycloak for user {UserId} ({KeycloakUserId}): {Error}. Role change will proceed.",
                    user.Id,
                    user.KeycloakUserId,
                    logoutResult.Error.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Unexpected error while invalidating Keycloak sessions for user {UserId} ({KeycloakUserId}). Role change will proceed.",
                user.Id,
                user.KeycloakUserId);
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
