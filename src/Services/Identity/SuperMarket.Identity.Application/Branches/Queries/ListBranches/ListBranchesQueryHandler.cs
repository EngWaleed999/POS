using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;

namespace SuperMarket.Identity.Application.Branches.Queries.ListBranches;

/// <summary>
/// Query handler that retrieves an offset-paginated stream of branch summaries.
/// Applies optional text search, status filters, deterministic sorting, and SQL projections.
/// </summary>
public sealed class ListBranchesQueryHandler : IPagedQueryHandler<ListBranchesQuery, BranchSummaryResponse>
{
    private readonly IIdentityReadDbContext _readDb;

    public ListBranchesQueryHandler(IIdentityReadDbContext readDb)
    {
        _readDb = readDb;
    }

    public async Task<Result<PagedList<BranchSummaryResponse>>> Handle(
        ListBranchesQuery query,
        CancellationToken cancellationToken)
    {
        var branches = _readDb.Branches;

        // 1. Text Search Filter (Case-insensitive across Name and BranchCode)
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            branches = branches.Where(b =>
                b.Name.ToLower().Contains(term) ||
                b.Code.Value.ToLower().Contains(term));
        }

        // 2. Operational Status Filter
        if (query.IsActive.HasValue)
        {
            branches = branches.Where(b => b.IsActive == query.IsActive.Value);
        }

        // 3. Deterministic Sorting & Direct SQL Projection
        var projectedQuery = branches
            .OrderBy(b => b.Name)
            .Select(b => new BranchSummaryResponse(
                Id: b.Id,
                Code: b.Code.Value,
                Name: b.Name,
                City: b.Address.City,
                Phone: b.Phone,
                Currency: b.Currency,
                IsActive: b.IsActive,
                CreatedAt: b.CreatedAt));

        // 4. Execute pagination via BuildingBlocks PagedList
        var pagedList = await PagedList<BranchSummaryResponse>.CreateAsync(
            projectedQuery,
            query.Pagination.PageNumber,
            query.Pagination.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success(pagedList);
    }
}
