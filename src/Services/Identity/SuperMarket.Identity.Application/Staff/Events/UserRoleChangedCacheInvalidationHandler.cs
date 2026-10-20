using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.Identity.Application.Abstractions.Authorization;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Application.Staff.Events;

/// <summary>
/// Domain event notification handler that clears the cached permissions of a staff member
/// when their role assignment is updated, guaranteeing immediate zero-delay permission revocation.
/// </summary>
public sealed class UserRoleChangedCacheInvalidationHandler : INotificationHandler<UserRoleChangedDomainEvent>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<UserRoleChangedCacheInvalidationHandler> _logger;

    public UserRoleChangedCacheInvalidationHandler(
        IPermissionService permissionService,
        ILogger<UserRoleChangedCacheInvalidationHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task Handle(UserRoleChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Invalidating permission cache for User {UserId} due to role change from {OldRole} to {NewRole}.",
            notification.UserId,
            notification.OldRoleId,
            notification.NewRoleId);

        await _permissionService.InvalidateUserPermissionsAsync(notification.UserId, cancellationToken);
    }
}
