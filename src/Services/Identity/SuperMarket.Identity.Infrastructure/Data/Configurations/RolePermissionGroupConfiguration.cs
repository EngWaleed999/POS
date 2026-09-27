using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class RolePermissionGroupConfiguration : IEntityTypeConfiguration<RolePermissionGroup>
{
    public void Configure(EntityTypeBuilder<RolePermissionGroup> builder)
    {
        builder.ToTable("role_permission_groups");

        builder.HasKey(x => new { x.RoleId, x.GroupId });

        builder.Property(x => x.RoleId)
            .IsRequired();

        builder.Property(x => x.GroupId)
            .IsRequired();

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PermissionGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign Key lookup index
        builder.HasIndex(x => x.GroupId);
    }
}
