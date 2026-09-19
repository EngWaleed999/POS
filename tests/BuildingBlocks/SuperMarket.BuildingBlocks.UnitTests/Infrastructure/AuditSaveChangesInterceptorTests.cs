using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Infrastructure;

namespace SuperMarket.BuildingBlocks.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="AuditSaveChangesInterceptor"/>
/// validating automated UTC timestamps, user attribution, soft-delete conversion,
/// and creation metadata immutability.
/// </summary>
public class AuditSaveChangesInterceptorTests
{
    // =========================================================================
    // 1. Added Entity: Automatic CreatedAt & CreatedBy
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ShouldPopulateCreatedAtAndCreatedBy_WhenEntityIsAdded()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedTime);

        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.Setup(u => u.UserId).Returns("USER-CASHIER-42");

        var interceptor = new AuditSaveChangesInterceptor(userContextMock.Object, timeProvider);
        await using var context = CreateTestDbContext(interceptor);

        var product = new AuditableProduct { Id = Guid.NewGuid(), Name = "Organic Milk" };
        context.Products.Add(product);

        // Act
        await context.SaveChangesAsync();

        // Assert
        product.CreatedAt.Should().Be(fixedTime);
        product.CreatedBy.Should().Be("USER-CASHIER-42");
        product.UpdatedAt.Should().BeNull();
        product.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldFallbackToSystemActor_WhenUserContextIsEmpty()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedTime);

        var interceptor = new AuditSaveChangesInterceptor(currentUserContext: null, timeProvider);
        await using var context = CreateTestDbContext(interceptor);

        var product = new AuditableProduct { Id = Guid.NewGuid(), Name = "System Seeded Bread" };
        context.Products.Add(product);

        // Act
        await context.SaveChangesAsync();

        // Assert: When user is null, defaults to SYSTEM
        product.CreatedBy.Should().Be("SYSTEM");
    }

    // =========================================================================
    // 2. Modified Entity: Automatic UpdatedAt, UpdatedBy & Creation Immutability
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ShouldPopulateUpdatedAtAndProtectCreationMetadata_WhenEntityIsModified()
    {
        // Arrange
        var initialTime = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var modifiedTime = new DateTimeOffset(2026, 9, 19, 15, 30, 0, TimeSpan.Zero);

        var timeProvider = new SettableTimeProvider(initialTime);

        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.Setup(u => u.UserId).Returns("INITIAL-CREATOR");

        var interceptor = new AuditSaveChangesInterceptor(userContextMock.Object, timeProvider);
        await using var context = CreateTestDbContext(interceptor);

        var product = new AuditableProduct { Id = Guid.NewGuid(), Name = "Cereal Box" };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Advance time and change user session
        timeProvider.CurrentTime = modifiedTime;
        userContextMock.Setup(u => u.UserId).Returns("EDITOR-USER-99");

        // Act: Modify the product and attempt malicious overwrite of CreatedAt/CreatedBy
        product.Name = "Cereal Box Family Size";
        product.CreatedAt = DateTimeOffset.MinValue;
        product.CreatedBy = "MALICIOUS-OVERWRITE";

        await context.SaveChangesAsync();

        // Assert
        product.UpdatedAt.Should().Be(modifiedTime);
        product.UpdatedBy.Should().Be("EDITOR-USER-99");

        // Critical Security Invariant: CreatedAt and CreatedBy must NEVER be overwritten on modification
        product.CreatedAt.Should().Be(initialTime);
        product.CreatedBy.Should().Be("INITIAL-CREATOR");
    }

    // =========================================================================
    // 3. Soft-Delete: Convert SQL DELETE to UPDATE with IsDeleted = true
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ShouldConvertDeletedEntityToModifiedWithSoftDeleteMetadata()
    {
        // Arrange
        var deleteTime = new DateTimeOffset(2026, 9, 19, 16, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(deleteTime);

        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.Setup(u => u.UserId).Returns("MANAGER-ADMIN");

        var interceptor = new AuditSaveChangesInterceptor(userContextMock.Object, timeProvider);
        await using var context = CreateTestDbContext(interceptor);

        var category = new SoftDeletableCategory { Id = Guid.NewGuid(), Title = "Discontinued Items" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        // Act: Issue a hard DELETE command through EF Core ChangeTracker
        context.Categories.Remove(category);
        await context.SaveChangesAsync();

        // Assert: State must be converted to Modified, not removed from database
        category.IsDeleted.Should().BeTrue();
        category.DeletedAt.Should().Be(deleteTime);
        category.DeletedBy.Should().Be("MANAGER-ADMIN");

        // Entity must still physically exist in database
        var persistedCategory = await context.Categories.FindAsync(category.Id);
        persistedCategory.Should().NotBeNull();
        persistedCategory!.IsDeleted.Should().BeTrue();
    }

    // =========================================================================
    // 4. Combined Auditable + Soft-Delete Entity Lifecycle
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ShouldHandleBothAuditAndSoftDelete_OnDualImplementingEntities()
    {
        // Arrange
        var fixedTime = new DateTimeOffset(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(fixedTime);

        var userContextMock = new Mock<ICurrentUserContext>();
        userContextMock.Setup(u => u.UserId).Returns("ADMIN-SUPER");

        var interceptor = new AuditSaveChangesInterceptor(userContextMock.Object, timeProvider);
        await using var context = CreateTestDbContext(interceptor);

        var item = new CompleteAuditedItem { Id = Guid.NewGuid(), Description = "Warehouse Pallet" };
        context.CompleteItems.Add(item);
        await context.SaveChangesAsync();

        // Act: Delete the item
        context.CompleteItems.Remove(item);
        await context.SaveChangesAsync();

        // Assert: Both soft-delete and update metadata are populated
        item.IsDeleted.Should().BeTrue();
        item.DeletedAt.Should().Be(fixedTime);
        item.DeletedBy.Should().Be("ADMIN-SUPER");
        item.UpdatedAt.Should().Be(fixedTime);
        item.UpdatedBy.Should().Be("ADMIN-SUPER");
    }

    // =========================================================================
    // Helpers & Test DbContext
    // =========================================================================

    private static TestAuditDbContext CreateTestDbContext(AuditSaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestAuditDbContext(options);
    }

    private class TestAuditDbContext : DbContext
    {
        public DbSet<AuditableProduct> Products => Set<AuditableProduct>();
        public DbSet<SoftDeletableCategory> Categories => Set<SoftDeletableCategory>();
        public DbSet<CompleteAuditedItem> CompleteItems => Set<CompleteAuditedItem>();

        public TestAuditDbContext(DbContextOptions<TestAuditDbContext> options) : base(options)
        {
        }
    }

    private class AuditableProduct : IAuditableEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    private class SoftDeletableCategory : ISoftDeletable
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    private class CompleteAuditedItem : IAuditableEntity, ISoftDeletable
    {
        public Guid Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    private class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _fixedTime;
        public FixedTimeProvider(DateTimeOffset fixedTime) => _fixedTime = fixedTime;
        public override DateTimeOffset GetUtcNow() => _fixedTime;
    }

    private class SettableTimeProvider : TimeProvider
    {
        public DateTimeOffset CurrentTime { get; set; }
        public SettableTimeProvider(DateTimeOffset initial) => CurrentTime = initial;
        public override DateTimeOffset GetUtcNow() => CurrentTime;
    }
}
