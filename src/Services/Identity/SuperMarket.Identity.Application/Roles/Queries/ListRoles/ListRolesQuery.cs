using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Roles.Queries.ListRoles;

/// <summary>
/// Query to retrieve an offset-paginated collection of roles with optional search and active status filtering.
/// Constrained via IPagedQuery to enforce defensive pagination and DTO-only responses.
/// </summary>
public sealed record ListRolesQuery(
    PaginationParams Pagination,
    string? SearchTerm = null,
    bool? IsActive = null) : IPagedQuery<RoleSummaryResponse>;
