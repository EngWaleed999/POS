using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoleName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(r => r.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        // Unique index on RoleName
        builder.HasIndex(r => r.RoleName)
            .IsUnique();

        // Performance index on IsActive
        builder.HasIndex(r => r.IsActive);
    }
}
