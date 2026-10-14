namespace SuperMarket.Identity.Application.Permissions.Queries.ListPermissionGroups;

/// <summary>
/// Detailed projection representing a permission group package along with its atomic permissions.
/// Feeds the role creation/edit screen with available package options.
/// </summary>
public sealed record PermissionGroupSummaryResponse(
    Guid Id,
    string GroupName,
    string Description,
    int PermissionsCount,
    IReadOnlyList<PermissionSummaryResponse> Permissions);
