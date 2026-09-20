// Domain event emitted when a user account is locked out after exceeding failed authentication attempts.

using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Identity.Domain.Events;

public sealed record UserLockedOutDomainEvent(
    Guid UserId,
    string Username,
    int FailedAttempts,
    DateTimeOffset LockoutEnd) : DomainEvent;
