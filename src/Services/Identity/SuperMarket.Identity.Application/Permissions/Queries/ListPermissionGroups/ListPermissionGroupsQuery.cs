using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Permissions.Queries.ListPermissionGroups;

/// <summary>
/// Query to retrieve all available permission groups and their constituent atomic permissions.
/// Used by the frontend to populate checkboxes and cards on the role creation/edit screens.
/// Implements IListQuery to retrieve unpaginated lookup collections with IResponseDto type enforcement.
/// </summary>
public sealed record ListPermissionGroupsQuery : IListQuery<PermissionGroupSummaryResponse>;
