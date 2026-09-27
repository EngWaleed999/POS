using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Infrastructure.Data.Configurations;

public sealed class POSRegisterConfiguration : IEntityTypeConfiguration<POSRegister>
{
    public void Configure(EntityTypeBuilder<POSRegister> builder)
    {
        builder.ToTable("pos_registers");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.BranchId)
            .IsRequired();

        builder.Property(r => r.RegisterCode)
            .HasConversion(
                code => code.Value,
                value => RegisterCode.Create(value).Value)
            .HasMaxLength(RegisterCode.MaxLength)
            .IsRequired();

        builder.Property(r => r.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.TerminalIpOrFingerprint)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(r => r.IsActive)
            .IsRequired();

        // Foreign Key index
        builder.HasIndex(r => r.BranchId);

        // Filtered composite unique index: RegisterCode is unique within a Branch among non-deleted registers
        builder.HasIndex(r => new { r.BranchId, r.RegisterCode })
            .IsUnique()
            .HasFilter("is_deleted = false");

        // Performance Indexes
        builder.HasIndex(r => r.IsActive);
        builder.HasIndex(r => r.IsDeleted);
    }
}
