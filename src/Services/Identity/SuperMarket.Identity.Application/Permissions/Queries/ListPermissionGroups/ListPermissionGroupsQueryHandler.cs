using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Abstractions.Persistence;

namespace SuperMarket.Identity.Application.Permissions.Queries.ListPermissionGroups;

/// <summary>
/// Query handler that retrieves all permission groups with their constituent permissions.
/// Executes two optimized SQL queries and joins them in memory to eliminate N+1 overhead.
/// </summary>
public sealed class ListPermissionGroupsQueryHandler 
    : IListQueryHandler<ListPermissionGroupsQuery, PermissionGroupSummaryResponse>
{
    private readonly IIdentityReadDbContext _readDb;

    public ListPermissionGroupsQueryHandler(IIdentityReadDbContext readDb)
    {
        _readDb = readDb;
    }

    public async Task<Result<IReadOnlyList<PermissionGroupSummaryResponse>>> Handle(
        ListPermissionGroupsQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Fetch all permission groups sorted by name
        var groups = await _readDb.PermissionGroups
            .OrderBy(g => g.GroupName)
            .Select(g => new
            {
                g.Id,
                g.GroupName,
                g.Description
            })
            .ToListAsync(cancellationToken);

        // 2. Fetch all group-permission mappings with atomic permission data
        var rawGroupPermissions = await (
            from pgi in _readDb.PermissionGroupItems
            join p in _readDb.Permissions on pgi.PermissionId equals p.Id
            orderby p.Resource, p.Action
            select new
            {
                pgi.GroupId,
                p.Id,
                p.Resource,
                p.Action,
                p.Scope,
                p.Category
            }
        ).ToListAsync(cancellationToken);

        // 3. Map to DTO in memory (computes canonical permission key)
        var groupPermissions = rawGroupPermissions.Select(p => new
        {
            p.GroupId,
            Permission = new PermissionSummaryResponse(
                Id: p.Id,
                Resource: p.Resource,
                Action: p.Action,
                Scope: p.Scope,
                Category: p.Category,
                Key: $"{p.Resource.ToLowerInvariant()}:{p.Action.ToLowerInvariant()}:{p.Scope.ToLowerInvariant()}")
        });

        // 4. Group permissions by GroupId in-memory lookup
        var permissionsLookup = groupPermissions.ToLookup(x => x.GroupId, x => x.Permission);

        // 5. Assemble final DTO response
        var response = groups.Select(g => new PermissionGroupSummaryResponse(
            Id: g.Id,
            GroupName: g.GroupName,
            Description: g.Description,
            PermissionsCount: permissionsLookup[g.Id].Count(),
            Permissions: permissionsLookup[g.Id].ToList()
        )).ToList();

        return Result.Success<IReadOnlyList<PermissionGroupSummaryResponse>>(response);
    }
}
