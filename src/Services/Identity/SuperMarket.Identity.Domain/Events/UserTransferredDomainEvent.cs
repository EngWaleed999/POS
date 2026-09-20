// Domain event emitted when a user is transferred or assigned to another branch.

using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Identity.Domain.Events;

public sealed record UserTransferredDomainEvent(
    Guid UserId,
    Guid? OldBranchId,
    Guid? NewBranchId) : DomainEvent;
