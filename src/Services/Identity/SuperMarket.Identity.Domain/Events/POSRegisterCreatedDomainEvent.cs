// Domain event emitted when a new POSRegister aggregate root is created.

using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.Identity.Domain.Events;

public sealed record POSRegisterCreatedDomainEvent(
    Guid RegisterId,
    Guid BranchId,
    string RegisterCode,
    string Name,
    string? TerminalIpOrFingerprint) : DomainEvent;
