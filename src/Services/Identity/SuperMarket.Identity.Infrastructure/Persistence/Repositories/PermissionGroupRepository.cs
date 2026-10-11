using Microsoft.EntityFrameworkCore;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Infrastructure.Data;

namespace SuperMarket.Identity.Infrastructure.Persistence.Repositories;

internal sealed class PermissionGroupRepository(IdentityDbContext dbContext)
    : Repository<PermissionGroup>(dbContext), IPermissionGroupRepository
{
    public async Task<bool> ExistsByNameAsync(string groupName, CancellationToken cancellationToken = default)
    {
        return await DbContext.PermissionGroups
            .AnyAsync(g => g.GroupName == groupName, cancellationToken);
    }

    public async Task<bool> AllExistAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);

        var distinctIds = groupIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return true;
        }

        var existingCount = await DbContext.PermissionGroups
            .CountAsync(g => distinctIds.Contains(g.Id), cancellationToken);

        return existingCount == distinctIds.Count;
    }
}
