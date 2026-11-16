// -------------------------------------------------------------------------
// Business Logic Flow:
// 1. [Audit & Observability]: Record security audit warning log for staff deactivation.
// 2. [Cache Invalidation & Blacklisting]: Evict cached permissions and set disabled tombstone in Redis.
// -------------------------------------------------------------------------

using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.Identity.Application.Abstractions.Authorization;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Application.Staff.EventHandlers;

public sealed class UserDeactivatedEventHandler : INotificationHandler<UserDeactivatedDomainEvent>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<UserDeactivatedEventHandler> _logger;

    public UserDeactivatedEventHandler(
        IPermissionService permissionService,
        ILogger<UserDeactivatedEventHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task Handle(UserDeactivatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // --- 1. Audit & Observability: Record security audit log ---
        _logger.LogWarning(
            "Domain Event Handled: Staff account deactivated. UserId: {UserId}",
            notification.UserId);

        // --- 2. Cache Invalidation & Blacklisting: Revoke Redis access ---
        await _permissionService.RevokeUserAccessAsync(notification.UserId, cancellationToken);
    }
}
