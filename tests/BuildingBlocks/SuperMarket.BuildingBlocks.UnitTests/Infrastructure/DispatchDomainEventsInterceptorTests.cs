using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Infrastructure;

namespace SuperMarket.BuildingBlocks.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="DispatchDomainEventsInterceptor"/>
/// validating event harvesting from Aggregate Roots, pre-dispatch clearing (replay defense),
/// and publishing through MediatR IPublisher.
/// </summary>
public class DispatchDomainEventsInterceptorTests
{
    // =========================================================================
    // 1. Single Aggregate Root with Domain Events
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ShouldDispatchAllEventsAndClearThem_WhenAggregateHasEvents()
    {
        // Arrange
        var publisherMock = new Mock<IPublisher>();
        var interceptor = new DispatchDomainEventsInterceptor(publisherMock.Object);
        await using var context = CreateTestDbContext(interceptor);

        var order = new TestOrderAggregate(Guid.NewGuid());
        var event1 = new TestOrderCreatedEvent(order.Id, 250m);
        var event2 = new TestOrderPaidEvent(order.Id);

        order.AddDomainEvent(event1);
        order.AddDomainEvent(event2);

        context.Orders.Add(order);

        // Act
        await context.SaveChangesAsync();

        // Assert:
        // 1. Events were published via IPublisher
        publisherMock.Verify(p => p.Publish<IDomainEvent>(event1, It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(p => p.Publish<IDomainEvent>(event2, It.IsAny<CancellationToken>()), Times.Once);

        // 2. Critical DDD Invariant: Events MUST be cleared from aggregate to prevent double-dispatch / replay
        order.DomainEvents.Should().BeEmpty();
    }

    // =========================================================================
    // 2. Aggregate Root with No Events (Bypass / No-Op)
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ShouldNotInvokePublisher_WhenAggregateHasNoEvents()
    {
        // Arrange
        var publisherMock = new Mock<IPublisher>();
        var interceptor = new DispatchDomainEventsInterceptor(publisherMock.Object);
        await using var context = CreateTestDbContext(interceptor);

        var order = new TestOrderAggregate(Guid.NewGuid());
        context.Orders.Add(order);

        // Act
        await context.SaveChangesAsync();

        // Assert
        publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // 3. Multiple Aggregates with Multiple Events
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ShouldDispatchEventsAcrossMultipleAggregatesInBatch()
    {
        // Arrange
        var publisherMock = new Mock<IPublisher>();
        var interceptor = new DispatchDomainEventsInterceptor(publisherMock.Object);
        await using var context = CreateTestDbContext(interceptor);

        var order1 = new TestOrderAggregate(Guid.NewGuid());
        var order2 = new TestOrderAggregate(Guid.NewGuid());

        var evt1 = new TestOrderCreatedEvent(order1.Id, 100m);
        var evt2 = new TestOrderCreatedEvent(order2.Id, 200m);

        order1.AddDomainEvent(evt1);
        order2.AddDomainEvent(evt2);

        context.Orders.AddRange(order1, order2);

        // Act
        await context.SaveChangesAsync();

        // Assert
        publisherMock.Verify(p => p.Publish<IDomainEvent>(evt1, It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(p => p.Publish<IDomainEvent>(evt2, It.IsAny<CancellationToken>()), Times.Once);

        order1.DomainEvents.Should().BeEmpty();
        order2.DomainEvents.Should().BeEmpty();
    }

    // =========================================================================
    // 4. Synchronous SaveChanges Invocation
    // =========================================================================

    [Fact]
    public void SaveChanges_ShouldDispatchEvents_WhenInvokedSynchronously()
    {
        // Arrange
        var publisherMock = new Mock<IPublisher>();
        var interceptor = new DispatchDomainEventsInterceptor(publisherMock.Object);
        using var context = CreateTestDbContext(interceptor);

        var order = new TestOrderAggregate(Guid.NewGuid());
        var evt = new TestOrderPaidEvent(order.Id);
        order.AddDomainEvent(evt);
        context.Orders.Add(order);

        // Act
        context.SaveChanges();

        // Assert
        publisherMock.Verify(p => p.Publish<IDomainEvent>(evt, It.IsAny<CancellationToken>()), Times.Once);
        order.DomainEvents.Should().BeEmpty();
    }

    // =========================================================================
    // Test Entities & DbContext
    // =========================================================================

    private static TestDomainEventsDbContext CreateTestDbContext(DispatchDomainEventsInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<TestDomainEventsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDomainEventsDbContext(options);
    }

    private class TestDomainEventsDbContext : DbContext
    {
        public DbSet<TestOrderAggregate> Orders => Set<TestOrderAggregate>();

        public TestDomainEventsDbContext(DbContextOptions<TestDomainEventsDbContext> options) : base(options)
        {
        }
    }

    private class TestOrderAggregate : AggregateRoot<Guid>
    {
        public TestOrderAggregate(Guid id) : base(id)
        {
        }
    }

    private record TestOrderCreatedEvent(Guid OrderId, decimal Total) : DomainEvent;
    private record TestOrderPaidEvent(Guid OrderId) : DomainEvent;
}
