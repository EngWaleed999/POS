
namespace SuperMarket.BuildingBlocks.Domain;

/// <summary>
/// Base class for Aggregate Roots in Domain-Driven Design.
/// Encapsulates domain event management and guarantees that events are only published from root entities.
/// </summary>
/// <typeparam name="TId">The type of the aggregate root's identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <inheritdoc />
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Initializes an aggregate root with a given identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>
    /// Registers a new domain event to be dispatched when the aggregate state is persisted.
    /// </summary>
    /// <param name="domainEvent">The domain event instance.</param>
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Removes a previously registered domain event.
    /// </summary>
    /// <param name="domainEvent">The domain event to remove.</param>
    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
