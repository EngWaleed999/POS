using Microsoft.EntityFrameworkCore;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.ValueObjects;
using SuperMarket.Identity.Infrastructure.Data;

namespace SuperMarket.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Specific repository implementation for the Branch Aggregate Root.
/// Inherits common persistence behaviors from Repository base class while providing full hydration (Eager Loading)
/// for child collections to enforce domain business rules.
/// </summary>
internal sealed class BranchRepository(IdentityDbContext dbContext)
    : Repository<Branch>(dbContext), IBranchRepository
{
    public override async Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Branches
            .Include(b => b.OperatingHours)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(BranchCode code, CancellationToken cancellationToken = default)
    {
        return await DbContext.Branches
            .AnyAsync(b => b.Code == code, cancellationToken);
    }
}
