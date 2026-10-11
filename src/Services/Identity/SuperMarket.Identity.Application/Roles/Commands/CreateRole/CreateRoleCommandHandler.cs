using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Roles.Commands.CreateRole;

/// <summary>
/// Orchestrates the creation of a dynamic job role and optional association with permission groups.
/// Enforces uniqueness constraints, validates group existence defensively, and commits atomically.
/// </summary>
public sealed class CreateRoleCommandHandler(
    IRoleRepository _roleRepository,
    IPermissionGroupRepository _permissionGroupRepository,
    IUnitOfWork _unitOfWork) : ICommandHandler<CreateRoleCommand, Guid>
{
    private readonly IRoleRepository _roleRepository = _roleRepository;
    private readonly IPermissionGroupRepository _permissionGroupRepository = _permissionGroupRepository;
    private readonly IUnitOfWork _unitOfWork = _unitOfWork;

    public async Task<Result<Guid>> Handle(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Enforce Uniqueness Invariant: RoleName must not already exist
        var nameExists = await _roleRepository.ExistsByNameAsync(command.RoleName, cancellationToken);
        if (nameExists)
        {
            return Result.Failure<Guid>(RoleErrors.NameAlreadyExists);
        }

        // 2. Validate existence of all requested PermissionGroupIds defensively
        if (command.PermissionGroupIds is not null && command.PermissionGroupIds.Count > 0)
        {
            var allGroupsExist = await _permissionGroupRepository.AllExistAsync(command.PermissionGroupIds, cancellationToken);
            if (!allGroupsExist)
            {
                return Result.Failure<Guid>(PermissionGroupErrors.OneOrMoreNotFound);
            }
        }

        // 3. Construct Entity via Domain Factory Method
        var roleResult = Role.Create(command.RoleName, command.Description);
        if (roleResult.IsFailure)
        {
            return Result.Failure<Guid>(roleResult.Error);
        }

        var role = roleResult.Value;

        // 4. Register Role in In-Memory ChangeTracker
        _roleRepository.Add(role);

        // 5. Construct and register join links if groups were provided
        if (command.PermissionGroupIds is not null && command.PermissionGroupIds.Count > 0)
        {
            var links = new List<RolePermissionGroup>(command.PermissionGroupIds.Count);
            foreach (var groupId in command.PermissionGroupIds.Distinct())
            {
                var linkResult = RolePermissionGroup.Create(role.Id, groupId);
                if (linkResult.IsFailure)
                {
                    return Result.Failure<Guid>(linkResult.Error);
                }

                links.Add(linkResult.Value);
            }

            _roleRepository.AddRolePermissionGroups(links);
        }

        // 6. Commit atomic transaction to database
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Return persistent Guid identifier
        return Result.Success(role.Id);
    }
}
