// Domain event emitted when a new Branch aggregate root is created.

using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Identity.Domain.Events;

public sealed record BranchCreatedDomainEvent(
    Guid BranchId,
    string BranchCode,
    string BranchName,
    string City,
    string Phone,
    string TaxNumber) : DomainEvent;
