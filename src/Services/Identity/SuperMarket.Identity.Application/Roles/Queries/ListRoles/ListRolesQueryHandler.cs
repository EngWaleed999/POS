using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;

namespace SuperMarket.Identity.Application.Roles.Queries.ListRoles;

/// <summary>
/// Query handler that retrieves an offset-paginated collection of role summaries.
/// Applies optional text search, status filters, deterministic sorting, and direct SQL projections.
/// </summary>
public sealed class ListRolesQueryHandler : IPagedQueryHandler<ListRolesQuery, RoleSummaryResponse>
{
    private readonly IIdentityReadDbContext _readDb;

    public ListRolesQueryHandler(IIdentityReadDbContext readDb)
    {
        _readDb = readDb;
    }

    public async Task<Result<PagedList<RoleSummaryResponse>>> Handle(
        ListRolesQuery query,
        CancellationToken cancellationToken)
    {
        var roles = _readDb.Roles;

        // 1. Case-insensitive Text Search Filter on RoleName
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            roles = roles.Where(r => r.RoleName.ToLower().Contains(term));
        }

        // 2. Operational Active Status Filter
        if (query.IsActive.HasValue)
        {
            roles = roles.Where(r => r.IsActive == query.IsActive.Value);
        }

        // 3. Deterministic Sorting & Direct SQL Projection (with correlated subquery for count)
        var projectedQuery = roles
            .OrderBy(r => r.RoleName)
            .Select(r => new RoleSummaryResponse(
              Id: r.Id,
                RoleName: r.RoleName,
                Description: r.Description,
                IsActive: r.IsActive,
                PermissionGroupsCount: _readDb.RolePermissionGroups.Count(rpg => rpg.RoleId == r.Id),
                CreatedAt: r.CreatedAt));

        // 4. Execute pagination via BuildingBlocks PagedList
        var pagedList = await PagedList<RoleSummaryResponse>.CreateAsync(
            projectedQuery,
            query.Pagination.PageNumber,
            query.Pagination.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success(pagedList);
    }
}
