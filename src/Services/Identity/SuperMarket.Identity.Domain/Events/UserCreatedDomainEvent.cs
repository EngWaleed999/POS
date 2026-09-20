// Domain event emitted when a new User aggregate root is created.

using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Identity.Domain.Events;

public sealed record UserCreatedDomainEvent(
    Guid UserId,
    string Username,
    string PhoneNumber,
    string? Email,
    string FullName,
    Guid RoleId,
    Guid? BranchId) : DomainEvent;
