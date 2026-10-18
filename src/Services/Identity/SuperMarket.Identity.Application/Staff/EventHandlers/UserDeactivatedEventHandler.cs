// Logs account deactivation events for administrative compliance and security audit trails.

using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Application.Staff.EventHandlers;

public sealed class UserDeactivatedEventHandler : INotificationHandler<UserDeactivatedDomainEvent>
{
    private readonly ILogger<UserDeactivatedEventHandler> _logger;

    public UserDeactivatedEventHandler(ILogger<UserDeactivatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserDeactivatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Domain Event Handled: Staff account deactivated. UserId: {UserId}",
            notification.UserId);

        return Task.CompletedTask;
    }
}
