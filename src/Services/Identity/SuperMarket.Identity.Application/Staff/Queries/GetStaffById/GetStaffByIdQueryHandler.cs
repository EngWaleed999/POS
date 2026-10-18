// Query handler retrieving staff details with role and branch projections.

using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Staff.Queries.GetStaffById;

public sealed class GetStaffByIdQueryHandler : IDtoQueryHandler<GetStaffByIdQuery, StaffDetailResponse>
{
    private readonly IIdentityReadDbContext _readDb;

    public GetStaffByIdQueryHandler(IIdentityReadDbContext readDb)
    {
        _readDb = readDb;
    }

    public async Task<Result<StaffDetailResponse>> Handle(GetStaffByIdQuery query, CancellationToken cancellationToken)
    {
        var staff = await (
            from u in _readDb.Users
            where u.Id == query.UserId
            join r in _readDb.Roles on u.RoleId equals r.Id
            join b in _readDb.Branches on u.BranchId equals b.Id into branchJoin
            from b in branchJoin.DefaultIfEmpty()
            select new StaffDetailResponse(
            Id:    u.Id,
            Username:    u.Username,
            FullName:    u.FullName,
            PhoneNumber:    u.PhoneNumber,
            Email:    u.Email,
            KeycloakUserId:    u.KeycloakUserId,
            HasPin:    u.PinHash != null,
            RoleId:    u.RoleId,
            RoleName:    r.RoleName,
            BranchId:    u.BranchId,
                b != null ? b.Name : null,
                u.IsActive,
                u.CreatedAt,
                u.LastLogin)
        ).FirstOrDefaultAsync(cancellationToken);

        if (staff is null)
        {
            return Result.Failure<StaffDetailResponse>(UserErrors.NotFound);
        }

        return Result.Success(staff);
    }
}
