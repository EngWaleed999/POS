using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.Identity.Infrastructure.Data;

namespace SuperMarket.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Base repository implementing boilerplate persistence operations (Add, Remove, default GetById)
/// for domain entities identified by a Guid.
/// Specific repositories inherit from this class to remain DRY while fulfilling explicit contracts.
/// </summary>
/// <typeparam name="TEntity">The domain entity or aggregate root type.</typeparam>
internal abstract class Repository<TEntity>(IdentityDbContext dbContext)
    where TEntity : Entity<Guid>
{
    protected readonly IdentityDbContext DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

   
    public void Add(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbContext.Set<TEntity>().Add(entity);
    }

    /// <summary>
    /// Marks an existing entity for deletion in the change tracker.
    /// In-memory operation; executed against database during SaveChangesAsync.
    /// </summary>
    public void Remove(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbContext.Set<TEntity>().Remove(entity);
    }

    /// <summary>
    /// Retrieves an entity by its identifier. Marked virtual to allow specific repositories
    /// to override and eagerly load required aggregate navigation properties (e.g. Include).
    /// </summary>
    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
    }
}
