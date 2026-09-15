using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.Infrastructure;

public sealed class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    // -------------------------------------------------------------------------
    // Dependencies & Injected Services
    // -------------------------------------------------------------------------
    private readonly IPublisher _publisher;

    public DispatchDomainEventsInterceptor(IPublisher publisher)
    {
        _publisher = publisher;
    }

    // -------------------------------------------------------------------------
    // EF Core Interception Hooks
    // -------------------------------------------------------------------------
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        DispatchDomainEventsAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Event Extraction & Dispatching Engine
    // -------------------------------------------------------------------------
    private async Task DispatchDomainEventsAsync(DbContext? context, CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            return;
        }

        var aggregateRoots = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        if (aggregateRoots.Count == 0)
        {
            return;
        }

        var domainEvents = aggregateRoots
            .SelectMany(root => root.DomainEvents)
            .ToList();

        foreach (var root in aggregateRoots)
        {
            root.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }
    }
}
