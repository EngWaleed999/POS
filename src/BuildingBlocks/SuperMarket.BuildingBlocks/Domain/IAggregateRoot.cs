
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.Domain
{
    
};

/// <summary>
/// Marker interface for Aggregate Roots in Domain-Driven Design.
/// Exposes domain event tracking for persistence interceptors and event dispatchers.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Read-only snapshot of all domain events queued by this aggregate root.
    /// </summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears all queued domain events after they are dispatched or saved to the outbox.
    /// </summary>
    void ClearDomainEvents();
}
