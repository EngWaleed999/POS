using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Resource)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Action)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Scope)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Category)
            .HasMaxLength(50)
            .IsRequired();

        // Unique composite index for atomic permission
        builder.HasIndex(p => new { p.Resource, p.Action, p.Scope })
            .IsUnique();

        // Index on Category for filtering permissions in UI
        builder.HasIndex(p => p.Category);
    }
}
