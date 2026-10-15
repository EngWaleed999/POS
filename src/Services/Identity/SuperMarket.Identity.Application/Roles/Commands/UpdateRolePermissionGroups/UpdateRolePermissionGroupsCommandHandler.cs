using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Roles.Commands.UpdateRolePermissionGroups;

/// <summary>
/// Command handler that synchronizes permission group packages for a role using in-memory state-diffing.
/// Enforces defensive validation (ADR-003) and commits minimal SQL deletions/insertions atomically.
/// </summary>
public sealed class UpdateRolePermissionGroupsCommandHandler(
    IRoleRepository roleRepository,
    IPermissionGroupRepository permissionGroupRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateRolePermissionGroupsCommand>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IPermissionGroupRepository _permissionGroupRepository = permissionGroupRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result> Handle(
        UpdateRolePermissionGroupsCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate Role existence
        var role = await _roleRepository.GetByIdAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound);
        }

        // 2. Preflight Validation (ADR-003): Ensure all target permission groups exist defensively
        if (command.PermissionGroupIds.Count > 0)
        {
            var allExist = await _permissionGroupRepository.AllExistAsync(command.PermissionGroupIds, cancellationToken);
            if (!allExist)
            {
                return Result.Failure(PermissionGroupErrors.OneOrMoreNotFound);
            }
        }

        // 3. Retrieve currently assigned join links from database (tracked by DbContext)
        var existingLinks = await _roleRepository.GetRolePermissionGroupsAsync(command.RoleId, cancellationToken);
        var existingGroupIds = existingLinks.Select(l => l.GroupId).ToHashSet();
        var targetGroupIds = command.PermissionGroupIds.ToHashSet();

        // 4. Compute State Diffing:
        // Identify links to remove (present in DB, absent in target)
        var linksToRemove = existingLinks
            .Where(l => !targetGroupIds.Contains(l.GroupId))
            .ToList();

        // Identify group IDs to add (present in target, absent in DB)
        var groupIdsToAdd = targetGroupIds
            .Where(id => !existingGroupIds.Contains(id))
            .ToList();

        // 5. Apply minimal mutations to repository change tracker
        if (linksToRemove.Count > 0)
        {
            _roleRepository.RemoveRolePermissionGroups(linksToRemove);
        }

        if (groupIdsToAdd.Count > 0)
        {
            var linksToAdd = new List<RolePermissionGroup>(groupIdsToAdd.Count);
            foreach (var groupId in groupIdsToAdd)
            {
                var linkResult = RolePermissionGroup.Create(
                    roleId: command.RoleId,
                    groupId: groupId);

                if (linkResult.IsFailure)
                {
                    return Result.Failure(linkResult.Error);
                }

                linksToAdd.Add(linkResult.Value);
            }

            _roleRepository.AddRolePermissionGroups(linksToAdd);
        }

        // 6. Commit atomic transaction if any changes occurred
        if (linksToRemove.Count > 0 || groupIdsToAdd.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
