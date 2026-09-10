
namespace SuperMarket.BuildingBlocks.Domain;

/// <summary>
/// Abstract base record for all domain events.
/// Using a record ensures immutability, value-based equality, and clean deconstruction.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
