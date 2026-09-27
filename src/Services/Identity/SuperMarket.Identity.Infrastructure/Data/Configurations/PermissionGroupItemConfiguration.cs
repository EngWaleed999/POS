using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class PermissionGroupItemConfiguration : IEntityTypeConfiguration<PermissionGroupItem>
{
    public void Configure(EntityTypeBuilder<PermissionGroupItem> builder)
    {
        builder.ToTable("permission_group_items");

        builder.HasKey(x => new { x.GroupId, x.PermissionId });

        builder.Property(x => x.GroupId)
            .IsRequired();

        builder.Property(x => x.PermissionId)
            .IsRequired();

        builder.HasOne<PermissionGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign Key lookup index
        builder.HasIndex(x => x.PermissionId);
    }
}
