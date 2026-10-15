using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Roles.Commands.UpdateRolePermissionGroups;

/// <summary>
/// Command to synchronize the complete set of permission groups assigned to a role (Full State Synchronization).
/// Replaces the assigned packages with the provided set via atomic state-diffing.
/// </summary>
public sealed record UpdateRolePermissionGroupsCommand(
    Guid RoleId,
    IReadOnlyList<Guid> PermissionGroupIds) : ICommand;
