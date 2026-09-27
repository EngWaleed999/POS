using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class BranchOperatingHoursConfiguration : IEntityTypeConfiguration<BranchOperatingHours>
{
    public void Configure(EntityTypeBuilder<BranchOperatingHours> builder)
    {
        builder.ToTable("branch_operating_hours");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.BranchId)
            .IsRequired();

        builder.Property(h => h.DayOfWeek)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(h => h.OpenTime)
            .IsRequired();

        builder.Property(h => h.CloseTime)
            .IsRequired();

        builder.Property(h => h.IsClosed)
            .IsRequired();

        // Computed Domain property - not mapped to DB
        builder.Ignore(h => h.IsOvernight);

        // Unique composite index: Each branch can only have one schedule per DayOfWeek
        builder.HasIndex(h => new { h.BranchId, h.DayOfWeek })
            .IsUnique();
    }
}
