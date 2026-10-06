using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Infrastructure;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Infrastructure.Data.Extensions;

namespace SuperMarket.Identity.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the Identity bounded context.
/// Implements IUnitOfWork for transactional command commitments,
/// and IIdentityReadDbContext with explicit non-tracking queryable sets for CQRS queries.
/// </summary>
public class IdentityDbContext : DbContext, IUnitOfWork, IIdentityReadDbContext
{
    public const string DefaultSchema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    // Write Sets (Tracked by default for repositories)
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchOperatingHours> BranchOperatingHours => Set<BranchOperatingHours>();
    public DbSet<POSRegister> POSRegisters => Set<POSRegister>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<PermissionGroupItem> PermissionGroupItems => Set<PermissionGroupItem>();
    public DbSet<RolePermissionGroup> RolePermissionGroups => Set<RolePermissionGroup>();
    public DbSet<User> Users => Set<User>();

    // IIdentityReadDbContext Implementation (Explicit No-Tracking for Read Queries)
    IQueryable<Branch> IIdentityReadDbContext.Branches => Set<Branch>().AsNoTracking();
    IQueryable<User> IIdentityReadDbContext.Users => Set<User>().AsNoTracking();
    IQueryable<Role> IIdentityReadDbContext.Roles => Set<Role>().AsNoTracking();
    IQueryable<Permission> IIdentityReadDbContext.Permissions => Set<Permission>().AsNoTracking();
    IQueryable<PermissionGroup> IIdentityReadDbContext.PermissionGroups => Set<PermissionGroup>().AsNoTracking();
    IQueryable<POSRegister> IIdentityReadDbContext.POSRegisters => Set<POSRegister>().AsNoTracking();

    // IUnitOfWork Implementation (Coordinates atomic commit for commands)
    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Isolate the Identity bounded context into its own PostgreSQL schema
        modelBuilder.HasDefaultSchema(DefaultSchema);

        // 2. Discover and register all IEntityTypeConfiguration<T> in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // 3. Register global soft-delete query filter (is_deleted = false)
        modelBuilder.ApplySoftDeleteQueryFilter();

        // 4. Register PostgreSQL-specific conventions (e.g. xmin optimistic concurrency token)
        modelBuilder.ApplyIdentityPostgresConventions();
    }
}
