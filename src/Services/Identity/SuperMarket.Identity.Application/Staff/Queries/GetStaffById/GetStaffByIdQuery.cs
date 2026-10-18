// Query to retrieve detailed information for a specific staff member.

using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Staff.Queries.GetStaffById;

public sealed record GetStaffByIdQuery(Guid UserId) : IDtoQuery<StaffDetailResponse>;
