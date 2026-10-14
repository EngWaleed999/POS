using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Roles.Queries.GetRoleById;

/// <summary>
/// Detailed projection representing a role with its associated permission group packages.
/// Feeds the role edit screen with current assignments.
/// Implements IResponseDto to satisfy query compile-time architectural constraints.
/// </summary>
public sealed record RoleDetailResponse(
    Guid Id,
    string RoleName,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RolePermissionGroupResponse> AssignedGroups) : IResponseDto;

public sealed record RolePermissionGroupResponse(
    Guid GroupId,
    string GroupName,
    string? Description,
    int PermissionsCount);
