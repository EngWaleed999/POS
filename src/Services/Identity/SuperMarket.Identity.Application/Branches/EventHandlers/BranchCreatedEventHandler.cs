using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Application.Branches.EventHandlers;

/// <summary>
/// Handles the BranchCreatedDomainEvent dispatched immediately after a branch is committed.
/// Serves as the extension point for cross-cutting notifications and outbox synchronization.
/// </summary>
public sealed class BranchCreatedEventHandler : INotificationHandler<BranchCreatedDomainEvent>
{
    private readonly ILogger<BranchCreatedEventHandler> _logger;

    public BranchCreatedEventHandler(ILogger<BranchCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(BranchCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Domain Event Handled: Branch created successfully. BranchId: {BranchId}, Code: {BranchCode}, Name: {BranchName}, City: {City}",
            notification.BranchId,
            notification.BranchCode,
            notification.BranchName,
            notification.City);

        return Task.CompletedTask;
    }
}
