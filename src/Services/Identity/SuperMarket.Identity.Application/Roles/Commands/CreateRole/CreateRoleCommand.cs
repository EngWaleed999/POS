using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Roles.Commands.CreateRole;

/// <summary>
/// Command to create a new job role in the enterprise retail platform.
/// Allows atomic association with existing permission group packages upon creation (One-Step Creation).
/// Returns the unique Guid identifier of the newly generated role upon success.
/// </summary>
public sealed record CreateRoleCommand(
    string RoleName,
    string? Description = null,
    IReadOnlyList<Guid>? PermissionGroupIds = null) : ICommand<Guid>;
