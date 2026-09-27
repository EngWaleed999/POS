using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
{
    public void Configure(EntityTypeBuilder<PermissionGroup> builder)
    {
        builder.ToTable("permission_groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.GroupName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.Description)
            .HasMaxLength(255)
            .IsRequired(false);

        // Unique index on GroupName
        builder.HasIndex(g => g.GroupName)
            .IsUnique();
    }
}
