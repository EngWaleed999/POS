using System.Collections;
using FluentAssertions;
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.UnitTests.Domain;

/// <summary>
/// Unit tests for <see cref="AggregateRoot{TId}"/> covering event lifecycle,
/// FIFO ordering, encapsulation protection, and null-guard invariants.
/// </summary>
public class AggregateRootTests
{
    // =========================================================================
    // 1. Invariant Guards: Null Checks
    // =========================================================================

    [Fact]
    public void AddDomainEvent_ShouldThrowArgumentNullException_WhenEventIsNull()
    {
        // Arrange
        var aggregate = new TestOrderAggregate(Guid.NewGuid());

        // Act
        Action act = () => aggregate.AddDomainEvent(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("domainEvent");
    }

    [Fact]
    public void RemoveDomainEvent_ShouldThrowArgumentNullException_WhenEventIsNull()
    {
        // Arrange
        var aggregate = new TestOrderAggregate(Guid.NewGuid());

        // Act
        Action act = () => aggregate.RemoveDomainEvent(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("domainEvent");
    }

    // =========================================================================
    // 2. Event Addition & FIFO Ordering ([Theory] with [InlineData])
    // =========================================================================

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void AddDomainEvent_ShouldAppendEventsInStrictFifoOrder(int eventCount)
    {
        // Arrange
        var aggregate = new TestOrderAggregate(Guid.NewGuid());
        var createdEvents = Enumerable.Range(1, eventCount)
            .Select(i => new OrderItemAddedEvent($"ITEM-{i}", i * 10))
            .ToList();

        // Act
        foreach (var @event in createdEvents)
        {
            aggregate.AddDomainEvent(@event);
        }

        // Assert
        aggregate.DomainEvents.Should().HaveCount(eventCount);
        aggregate.DomainEvents.Should().ContainInOrder(createdEvents);
    }

    // =========================================================================
    // 3. Event Removal Behavior ([Theory] with [InlineData])
    // =========================================================================

    [Theory]
    [InlineData(true)]  // Existing event in collection
    [InlineData(false)] // Non-existing event (should be a safe no-op)
    public void RemoveDomainEvent_ShouldHandleExistingAndNonExistingEvents(bool existsInCollection)
    {
        // Arrange
        var aggregate = new TestOrderAggregate(Guid.NewGuid());
        var initialEvent1 = new OrderItemAddedEvent("ITEM-01", 100);
        var initialEvent2 = new OrderItemAddedEvent("ITEM-02", 200);

        aggregate.AddDomainEvent(initialEvent1);
        aggregate.AddDomainEvent(initialEvent2);

        var targetEvent = existsInCollection
            ? initialEvent1
            : new OrderItemAddedEvent("ITEM-UNTRACKED", 999);

        // Act
        aggregate.RemoveDomainEvent(targetEvent);

        // Assert
        if (existsInCollection)
        {
            aggregate.DomainEvents.Should().HaveCount(1);
            aggregate.DomainEvents.Should().NotContain(initialEvent1);
            aggregate.DomainEvents.Should().Contain(initialEvent2);
        }
        else
        {
            aggregate.DomainEvents.Should().HaveCount(2);
            aggregate.DomainEvents.Should().ContainInOrder(initialEvent1, initialEvent2);
        }
    }

    // =========================================================================
    // 4. ClearDomainEvents Replay Prevention ([Theory] with [InlineData])
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ClearDomainEvents_ShouldEmptyCollection_PreventingEventReplay(int initialCount)
    {
        // Arrange
        var aggregate = new TestOrderAggregate(Guid.NewGuid());
        for (var i = 0; i < initialCount; i++)
        {
            aggregate.AddDomainEvent(new OrderItemAddedEvent($"ITEM-{i}", 50));
        }

        aggregate.DomainEvents.Should().HaveCount(initialCount);

        // Act: Cleared typically by outbox interceptor or unit of work after dispatch
        aggregate.ClearDomainEvents();

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }

    // =========================================================================
    // 5. Encapsulation Leak Protection (Critical DDD Invariant)
    // =========================================================================

    [Fact]
    public void DomainEvents_ShouldBeProtectedAgainstExternalMutationViaCasting()
    {
        // Arrange
        var aggregate = new TestOrderAggregate(Guid.NewGuid());
        var initialEvent = new OrderItemAddedEvent("ITEM-A", 100);
        aggregate.AddDomainEvent(initialEvent);

        var readOnlyEvents = aggregate.DomainEvents;

        // Act & Assert: Attempting to cast to mutable ICollection or IList must throw NotSupportedException
        var mutableCollection = readOnlyEvents as ICollection<IDomainEvent>;
        mutableCollection.Should().NotBeNull();

        var unauthorizedEvent = new OrderItemAddedEvent("MALICIOUS-ITEM", 0);
        Action addAction = () => mutableCollection!.Add(unauthorizedEvent);
        Action clearAction = () => mutableCollection!.Clear();

        addAction.Should().ThrowExactly<NotSupportedException>();
        clearAction.Should().ThrowExactly<NotSupportedException>();

        // Verify state remains untampered
        aggregate.DomainEvents.Should().HaveCount(1);
        aggregate.DomainEvents.Should().Contain(initialEvent);
    }

    // =========================================================================
    // 6. Inherited Identity Contract from Entity<TId>
    // =========================================================================

    [Fact]
    public void AggregateRoot_ShouldRetainEntityIdentitySemantics()
    {
        // Arrange
        var sharedId = Guid.NewGuid();
        var aggregate1 = new TestOrderAggregate(sharedId);
        var aggregate2 = new TestOrderAggregate(sharedId);
        var differentAggregate = new TestOrderAggregate(Guid.NewGuid());

        // Act & Assert
        aggregate1.Equals(aggregate2).Should().BeTrue();
        (aggregate1 == aggregate2).Should().BeTrue();

        aggregate1.Equals(differentAggregate).Should().BeFalse();
        (aggregate1 == differentAggregate).Should().BeFalse();
    }

    // =========================================================================
    // 7. EF Core Compatibility: Parameterless Constructor
    // =========================================================================

    [Fact]
    public void ParameterlessConstructor_ShouldInitializeSuccessfully_ForEfCoreMaterialization()
    {
        // Arrange & Act
        var aggregate = new TestOrderAggregate();

        // Assert
        aggregate.DomainEvents.Should().NotBeNull().And.BeEmpty();
        aggregate.IsTransient().Should().BeTrue();
    }

    // =========================================================================
    // Test Fakes
    // =========================================================================

    private class TestOrderAggregate : AggregateRoot<Guid>
    {
        public TestOrderAggregate()
        {
        }

        public TestOrderAggregate(Guid id) : base(id)
        {
        }
    }

    private record OrderItemAddedEvent(string ItemCode, decimal Price) : DomainEvent;
}
