using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Infrastructure;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Infrastructure.Data.Extensions;

namespace SuperMarket.Identity.Infrastructure.Data;

public class IdentityDbContext : DbContext
{
    public const string DefaultSchema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchOperatingHours> BranchOperatingHours => Set<BranchOperatingHours>();
    public DbSet<POSRegister> POSRegisters => Set<POSRegister>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<PermissionGroupItem> PermissionGroupItems => Set<PermissionGroupItem>();
    public DbSet<RolePermissionGroup> RolePermissionGroups => Set<RolePermissionGroup>();
    public DbSet<User> Users => Set<User>();

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
