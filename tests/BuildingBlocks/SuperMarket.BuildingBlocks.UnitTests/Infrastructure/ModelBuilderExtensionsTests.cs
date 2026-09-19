using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Infrastructure;

namespace SuperMarket.BuildingBlocks.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="ModelBuilderExtensions.ApplySoftDeleteQueryFilter"/>
/// validating automated HasQueryFilter application on all ISoftDeletable entities.
/// </summary>
public class ModelBuilderExtensionsTests
{
    // =========================================================================
    // 1. Automatic Soft-Delete Filtering in Queries
    // =========================================================================

    [Fact]
    public async Task ApplySoftDeleteQueryFilter_ShouldExcludeSoftDeletedEntitiesFromDefaultQueries()
    {
        // Arrange
        await using var context = CreateTestDbContext();

        var activeSupplier = new TestSupplier { Id = Guid.NewGuid(), Name = "Active Farm", IsDeleted = false };
        var deletedSupplier = new TestSupplier { Id = Guid.NewGuid(), Name = "Closed Factory", IsDeleted = true };

        context.Suppliers.AddRange(activeSupplier, deletedSupplier);
        await context.SaveChangesAsync();

        // Act: Standard LINQ query without explicit filtering
        var retrievedSuppliers = await context.Suppliers.ToListAsync();

        // Assert: Must automatically exclude deleted records
        retrievedSuppliers.Should().HaveCount(1);
        retrievedSuppliers.Should().ContainSingle(s => s.Id == activeSupplier.Id);
        retrievedSuppliers.Should().NotContain(s => s.Id == deletedSupplier.Id);
    }

    // =========================================================================
    // 2. Bypassing Soft-Delete Filter via IgnoreQueryFilters()
    // =========================================================================

    [Fact]
    public async Task ApplySoftDeleteQueryFilter_ShouldAllowRetrievalOfDeletedEntities_WhenIgnoreQueryFiltersIsUsed()
    {
        // Arrange
        await using var context = CreateTestDbContext();

        var activeSupplier = new TestSupplier { Id = Guid.NewGuid(), Name = "Fresh Dairy", IsDeleted = false };
        var deletedSupplier = new TestSupplier { Id = Guid.NewGuid(), Name = "Old Bakery", IsDeleted = true };

        context.Suppliers.AddRange(activeSupplier, deletedSupplier);
        await context.SaveChangesAsync();

        // Act: Administrative query explicitly bypassing filters
        var allSuppliers = await context.Suppliers.IgnoreQueryFilters().ToListAsync();

        // Assert: Both active and soft-deleted entities are returned
        allSuppliers.Should().HaveCount(2);
        allSuppliers.Should().Contain(s => s.Id == activeSupplier.Id);
        allSuppliers.Should().Contain(s => s.Id == deletedSupplier.Id);
    }

    // =========================================================================
    // 3. Unrelated Non-SoftDeletable Entities Are Unaffected
    // =========================================================================

    [Fact]
    public async Task ApplySoftDeleteQueryFilter_ShouldNotAffectEntitiesNotImplementingISoftDeletable()
    {
        // Arrange
        await using var context = CreateTestDbContext();

        var tag1 = new TestTag { Id = Guid.NewGuid(), Label = "PROMO" };
        var tag2 = new TestTag { Id = Guid.NewGuid(), Label = "CLEARANCE" };

        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();

        // Act
        var tags = await context.Tags.ToListAsync();

        // Assert
        tags.Should().HaveCount(2);
    }

    // =========================================================================
    // 4. Invariant: Null ModelBuilder Guard
    // =========================================================================

    [Fact]
    public void ApplySoftDeleteQueryFilter_ShouldThrowArgumentNullException_WhenModelBuilderIsNull()
    {
        // Arrange
        ModelBuilder nullBuilder = null!;

        // Act
        Action act = () => nullBuilder.ApplySoftDeleteQueryFilter();

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("modelBuilder");
    }

    // =========================================================================
    // Helpers & Test DbContext
    // =========================================================================

    private static SoftDeleteTestDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<SoftDeleteTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SoftDeleteTestDbContext(options);
    }

    private class SoftDeleteTestDbContext : DbContext
    {
        public DbSet<TestSupplier> Suppliers => Set<TestSupplier>();
        public DbSet<TestTag> Tags => Set<TestTag>();

        public SoftDeleteTestDbContext(DbContextOptions<SoftDeleteTestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplySoftDeleteQueryFilter();
        }
    }

    private class TestSupplier : ISoftDeletable
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    private class TestTag
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
