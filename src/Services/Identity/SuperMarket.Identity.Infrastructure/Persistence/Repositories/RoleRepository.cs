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
}
