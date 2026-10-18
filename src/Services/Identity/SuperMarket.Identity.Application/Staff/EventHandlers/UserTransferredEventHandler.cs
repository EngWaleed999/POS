// Logs staff branch reassignment events for audit and security monitoring.

using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Application.Staff.EventHandlers;

public sealed class UserTransferredEventHandler : INotificationHandler<UserTransferredDomainEvent>
{
    private readonly ILogger<UserTransferredEventHandler> _logger;

    public UserTransferredEventHandler(ILogger<UserTransferredEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserTransferredDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Domain Event Handled: Staff branch transferred. UserId: {UserId}, OldBranchId: {OldBranchId}, NewBranchId: {NewBranchId}",
            notification.UserId,
            notification.OldBranchId,
            notification.NewBranchId);

        return Task.CompletedTask;
    }
}
