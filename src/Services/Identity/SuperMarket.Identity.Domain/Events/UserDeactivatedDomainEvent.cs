// Domain event emitted when a user account is deactivated or soft-deleted.

using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Identity.Domain.Events;

public sealed record UserDeactivatedDomainEvent(
    Guid UserId) : DomainEvent;
