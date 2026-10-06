using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Queries.ListBranches;

/// <summary>
/// Query to retrieve an offset-paginated collection of branches with optional text search and active status filtering.
/// Strictly constrained via IPagedQuery to enforce DTO responses and defensive pagination.
/// </summary>
public sealed record ListBranchesQuery(
    PaginationParams Pagination,
    string? SearchTerm = null,
    bool? IsActive = null) : IPagedQuery<BranchSummaryResponse>;
