using FluentAssertions;
using MediatR;
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.UnitTests.Domain;

/// <summary>
/// Unit tests for <see cref="DomainEvent"/> abstract base record,
/// validating event metadata generation, immutability, record equality,
/// and MediatR INotification contracts.
/// </summary>
public class DomainEventTests
{
    // =========================================================================
    // 1. Metadata Generation (EventId & OccurredOn)
    // =========================================================================

    [Fact]
    public void Constructor_ShouldInitializeUniqueNonEmptyEventId()
    {
        // Arrange & Act
        var event1 = new ProductPriceChangedEvent("PROD-01", 99.99m);
        var event2 = new ProductPriceChangedEvent("PROD-01", 99.99m);

        // Assert
        event1.EventId.Should().NotBeEmpty();
        event2.EventId.Should().NotBeEmpty();
        event1.EventId.Should().NotBe(event2.EventId, "each new domain event instance must generate a unique UUID for idempotency tracking");
    }

    [Fact]
    public void Constructor_ShouldInitializeOccurredOnWithCurrentUtcTimestamp()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var @event = new ProductPriceChangedEvent("PROD-01", 99.99m);

        // Assert
        var after = DateTimeOffset.UtcNow;
        @event.OccurredOn.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        @event.OccurredOn.Offset.Should().Be(TimeSpan.Zero, "domain event timestamps must be strictly in UTC");
    }

    // =========================================================================
    // 2. MediatR Contract Compliance
    // =========================================================================

    [Fact]
    public void DomainEvent_ShouldImplementIDomainEventAndINotification()
    {
        // Arrange
        var @event = new ProductPriceChangedEvent("PROD-01", 99.99m);

        // Assert
        @event.Should().BeAssignableTo<IDomainEvent>();
        @event.Should().BeAssignableTo<INotification>();
    }

    // =========================================================================
    // 3. Parameterized Record Equality ([Theory] with [InlineData])
    // =========================================================================

    [Theory]
    [InlineData("SKU-100", 50.0, "SKU-100", 50.0, true)]
    [InlineData("SKU-100", 50.0, "SKU-200", 50.0, false)]
    [InlineData("SKU-100", 50.0, "SKU-100", 75.0, false)]
    public void RecordEquality_ShouldEvaluateCorrectly_WhenComparedWithSameMetadata(
        string sku1, decimal price1, string sku2, decimal price2, bool expectedEqual)
    {
        // Arrange: Use deterministic EventId and Timestamp to isolate payload evaluation
        var fixedId = Guid.NewGuid();
        var fixedTimestamp = DateTimeOffset.UtcNow;

        var event1 = new ProductPriceChangedEvent(sku1, price1)
        {
            EventId = fixedId,
            OccurredOn = fixedTimestamp
        };

        var event2 = new ProductPriceChangedEvent(sku2, price2)
        {
            EventId = fixedId,
            OccurredOn = fixedTimestamp
        };

        // Act & Assert
        event1.Equals(event2).Should().Be(expectedEqual);
        (event1 == event2).Should().Be(expectedEqual);
        (event1 != event2).Should().Be(!expectedEqual);

        if (expectedEqual)
        {
            event1.GetHashCode().Should().Be(event2.GetHashCode());
        }
    }

    // =========================================================================
    // 4. Non-Destructive Mutation (`with` expression)
    // =========================================================================

    [Fact]
    public void WithExpression_ShouldProduceNewInstance_LeavingOriginalImmutable()
    {
        // Arrange
        var original = new ProductPriceChangedEvent("SKU-01", 100m);

        // Act
        var modified = original with { NewPrice = 150m };

        // Assert
        original.NewPrice.Should().Be(100m, "original domain event record must be completely immutable");
        modified.NewPrice.Should().Be(150m);
        modified.Sku.Should().Be(original.Sku);
        modified.EventId.Should().Be(original.EventId);
        modified.OccurredOn.Should().Be(original.OccurredOn);
        modified.Should().NotBeSameAs(original);
    }

    // =========================================================================
    // Test Fakes
    // =========================================================================

    private record ProductPriceChangedEvent(string Sku, decimal NewPrice) : DomainEvent;
}
