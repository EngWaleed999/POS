using MediatR;

namespace SuperMarket.BuildingBlocks.Domain;


public interface IDomainEvent : INotification
{
    /// <summary>
    /// Unique identifier for the event instance. Crucial for idempotency and deduplication.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// The exact UTC timestamp with offset when the domain event occurred.
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}
