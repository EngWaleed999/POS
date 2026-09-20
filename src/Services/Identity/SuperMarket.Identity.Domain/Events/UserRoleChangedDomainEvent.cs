// Domain event emitted when a user's role is updated.

using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Identity.Domain.Events;

public sealed record UserRoleChangedDomainEvent(
    Guid UserId,
    Guid OldRoleId,
    Guid NewRoleId) : DomainEvent;
