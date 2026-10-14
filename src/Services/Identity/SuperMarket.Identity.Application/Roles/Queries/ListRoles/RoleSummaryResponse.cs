using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Roles.Queries.ListRoles;

/// <summary>
/// Lightweight, flat projection representing a role summary for tabular display and dropdown lookups.
/// Implements IResponseDto to satisfy query compile-time architectural constraints.
/// </summary>
public sealed record RoleSummaryResponse(
    Guid Id,
    string RoleName,
    string? Description,
    bool IsActive,
    int PermissionGroupsCount,
    DateTimeOffset CreatedAt) : IResponseDto;
