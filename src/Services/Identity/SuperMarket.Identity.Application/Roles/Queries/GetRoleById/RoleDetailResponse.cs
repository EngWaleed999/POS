namespace SuperMarket.Identity.Application.Roles.Queries.GetRoleById;

/// <summary>
/// Detailed projection representing a role with its associated permission group packages.
/// Feeds the role edit screen with current assignments.
/// </summary>
public sealed record RoleDetailResponse(
    Guid Id,
    string RoleName,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RolePermissionGroupResponse> AssignedGroups);

public sealed record RolePermissionGroupResponse(
    Guid GroupId,
    string GroupName,
    string Description,
    int PermissionsCount);
