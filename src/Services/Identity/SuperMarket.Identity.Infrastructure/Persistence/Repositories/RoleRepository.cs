using Microsoft.EntityFrameworkCore;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Infrastructure.Data;

namespace SuperMarket.Identity.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository(IdentityDbContext dbContext)
    : Repository<Role>(dbContext), IRoleRepository
{
    public async Task<bool> ExistsByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await DbContext.Roles
            .AnyAsync(r => r.RoleName == roleName, cancellationToken);
    }

    public void AddRolePermissionGroups(IEnumerable<RolePermissionGroup> rolePermissionGroups)
    {
        ArgumentNullException.ThrowIfNull(rolePermissionGroups);
        DbContext.RolePermissionGroups.AddRange(rolePermissionGroups);
    }

    public async Task<List<RolePermissionGroup>> GetRolePermissionGroupsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await DbContext.RolePermissionGroups
            .Where(rpg => rpg.RoleId == roleId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveRolePermissionGroups(IEnumerable<RolePermissionGroup> rolePermissionGroups)
    {
        ArgumentNullException.ThrowIfNull(rolePermissionGroups);
        DbContext.RolePermissionGroups.RemoveRange(rolePermissionGroups);
    }
}
