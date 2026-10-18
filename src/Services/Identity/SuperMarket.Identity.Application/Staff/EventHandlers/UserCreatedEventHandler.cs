// Logs audit trails and triggers cross-boundary workflows upon staff creation.

using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Application.Staff.EventHandlers;

public sealed class UserCreatedEventHandler : INotificationHandler<UserCreatedDomainEvent>
{
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Domain Event Handled: Staff created. UserId: {UserId}, Username: {Username}, FullName: {FullName}, RoleId: {RoleId}, BranchId: {BranchId}",
            notification.UserId,
            notification.Username,
            notification.FullName,
            notification.RoleId,
            notification.BranchId);

        return Task.CompletedTask;
    }
}
