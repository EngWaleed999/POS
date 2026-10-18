// Query to retrieve an offset-paginated collection of staff members with optional filters.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Queries.ListStaff;

public sealed record ListStaffQuery(
    PaginationParams Pagination,
    Guid? BranchId = null,
    Guid? RoleId = null,
    bool? IsActive = null,
    string? SearchTerm = null) : IPagedQuery<StaffSummaryResponse>;
