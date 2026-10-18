// Query handler for retrieving paginated staff listings with multi-criteria filtering.

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;

namespace SuperMarket.Identity.Application.Staff.Queries.ListStaff;

public sealed class ListStaffQueryHandler : IPagedQueryHandler<ListStaffQuery, StaffSummaryResponse>
{
    private readonly IIdentityReadDbContext _readDb;

    public ListStaffQueryHandler(IIdentityReadDbContext readDb)
    {
        _readDb = readDb;
    }

    public async Task<Result<PagedList<StaffSummaryResponse>>> Handle(ListStaffQuery query, CancellationToken cancellationToken)
    {
        var queryable = from u in _readDb.Users
                        join r in _readDb.Roles on u.RoleId equals r.Id
                        join b in _readDb.Branches on u.BranchId equals b.Id into branchJoin
                        from b in branchJoin.DefaultIfEmpty()
                        select new { User = u, RoleName = r.RoleName, BranchName = b != null ? b.Name : null };

        // --- 1. Branch Filter ---
        if (query.BranchId.HasValue)
        {
            queryable = queryable.Where(x => x.User.BranchId == query.BranchId.Value);
        }

        // --- 2. Role Filter ---
        if (query.RoleId.HasValue)
        {
            queryable = queryable.Where(x => x.User.RoleId == query.RoleId.Value);
        }

        // --- 3. Active Status Filter ---
        if (query.IsActive.HasValue)
        {
            queryable = queryable.Where(x => x.User.IsActive == query.IsActive.Value);
        }

        // --- 4. Search Filter (Username, FullName, Phone) ---
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            queryable = queryable.Where(x =>
                x.User.Username.ToLower().Contains(term) ||
                x.User.FullName.ToLower().Contains(term) ||
                x.User.PhoneNumber.ToLower().Contains(term));
        }

        // --- 5. Deterministic Projection & Pagination ---
        var projected = queryable
            .OrderBy(x => x.User.FullName)
            .Select(x => new StaffSummaryResponse(
                x.User.Id,
                x.User.Username,
                x.User.FullName,
                x.User.PhoneNumber,
                x.User.Email,
                x.User.RoleId,
                x.RoleName,
                x.User.BranchId,
                x.BranchName,
                x.User.PinHash != null,
                x.User.IsActive,
                x.User.CreatedAt));

        var pagedList = await PagedList<StaffSummaryResponse>.CreateAsync(
            projected,
            query.Pagination.PageNumber,
            query.Pagination.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success(pagedList);
    }
}
