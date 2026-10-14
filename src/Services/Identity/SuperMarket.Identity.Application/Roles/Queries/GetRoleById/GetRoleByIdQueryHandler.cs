using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Application.Roles.Queries.GetRoleById;

/// <summary>
/// Query handler that retrieves role details directly from the read-optimized DbContext.
/// Performs two efficient queries: fetches role metadata, then joins assigned groups with permission counts.
/// </summary>
public sealed class GetRoleByIdQueryHandler : IDtoQueryHandler<GetRoleByIdQuery, RoleDetailResponse>
{
    private readonly IIdentityReadDbContext _readDb;

    public GetRoleByIdQueryHandler(IIdentityReadDbContext readDb)
    {
        _readDb = readDb;
    }

    public async Task<Result<RoleDetailResponse>> Handle(
        GetRoleByIdQuery query,
        CancellationToken cancellationToken)
    {
        var role = await _readDb.Roles
            .Where(r => r.Id == query.RoleId)
            .Select(r => new
            {
                r.Id,
                r.RoleName,
                r.Description,
                r.IsActive,
                r.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (role is null)
        {
            return Result.Failure<RoleDetailResponse>(RoleErrors.NotFound);
        }

        // Fetch assigned permission groups with their permission count
        var assignedGroups = await (
            from rpg in _readDb.RolePermissionGroups
            where rpg.RoleId == query.RoleId
            join g in _readDb.PermissionGroups on rpg.GroupId equals g.Id
            orderby g.GroupName
            select new RolePermissionGroupResponse(
                GroupId: g.Id,
                GroupName: g.GroupName,
                Description: g.Description,
                PermissionsCount: _readDb.PermissionGroupItems.Count(pgi => pgi.GroupId == g.Id)
            )
        ).ToListAsync(cancellationToken);

        var response = new RoleDetailResponse(
            Id: role.Id,
            RoleName: role.RoleName,
            Description: role.Description,
            IsActive: role.IsActive,
            CreatedAt: role.CreatedAt,
            AssignedGroups: assignedGroups);

        return Result.Success(response);
    }
}
