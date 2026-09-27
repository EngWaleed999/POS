using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.KeycloakUserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.PinHash)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(u => u.RoleId)
            .IsRequired();

        builder.Property(u => u.BranchId)
            .IsRequired(false);

        builder.Property(u => u.AccessFailedCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(u => u.LastLogin)
            .IsRequired(false);

        builder.Property(u => u.LockoutEnd)
            .IsRequired(false);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(u => u.DeletedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.DeletionReason)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(u => u.CreatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.UpdatedBy)
            .HasMaxLength(100)
            .IsRequired(false);

        // Relationships
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(u => u.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Filtered Unique Indexes (Only active non-deleted records)
        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(u => u.KeycloakUserId)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("is_deleted = false AND email IS NOT NULL");

        // Performance & Foreign Key Indexes
        builder.HasIndex(u => u.BranchId);
        builder.HasIndex(u => u.RoleId);
        builder.HasIndex(u => new { u.BranchId, u.RoleId });
        builder.HasIndex(u => u.IsActive);
        builder.HasIndex(u => u.IsDeleted);
    }
}
