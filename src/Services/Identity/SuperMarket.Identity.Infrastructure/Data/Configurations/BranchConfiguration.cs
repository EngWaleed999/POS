using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Code)
            .HasConversion(
                code => code.Value,
                value => BranchCode.Create(value).Value)
            .HasMaxLength(BranchCode.MaxLength)
            .IsRequired();

        builder.Property(b => b.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.Phone)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.Email)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(b => b.TaxNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Currency)
            .HasMaxLength(10)
            .HasDefaultValue("YE")
            .IsRequired();

        builder.Property(b => b.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        // Owned Value Object: Address
        builder.OwnsOne(b => b.Address, addressBuilder =>
        {
            addressBuilder.Property(a => a.Street)
                .HasColumnName("street")
                .HasMaxLength(150)
                .IsRequired();

            addressBuilder.Property(a => a.City)
                .HasColumnName("city")
                .HasMaxLength(50)
                .IsRequired();

            addressBuilder.Property(a => a.Region)
                .HasColumnName("region")
                .HasMaxLength(50)
                .IsRequired();

            addressBuilder.Property(a => a.PostalCode)
                .HasColumnName("postal_code")
                .HasMaxLength(20)
                .IsRequired(false);
        });

        // Child Collection: Operating Hours (Aggregate boundary)
        builder.HasMany(b => b.OperatingHours)
            .WithOne()
            .HasForeignKey(h => h.BranchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.OperatingHours)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Filtered Unique Indexes (Only active non-deleted records)
        builder.HasIndex(b => b.Code)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(b => b.TaxNumber)
            .IsUnique()
            .HasFilter("is_deleted = false");

        // Performance Indexes
        builder.HasIndex(b => b.IsActive);
        builder.HasIndex(b => b.IsDeleted);
    }
}
