using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SuperMarket.Identity.Application.Abstractions.Authorization;

namespace SuperMarket.Identity.API.Authorization;

/// <summary>
/// Evaluates PermissionAuthorizationRequirement instances using IPermissionService (Cache-Aside Redis).
/// Extracts the current user identity from claims and verifies atomic permissions in sub-milliseconds.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionAuthorizationRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionAuthorizationRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated is not true)
        {
            return;
        }

        var userIdString = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue("user_id");

        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            _logger.LogWarning("Authorization failed: Missing or invalid user identifier claim in token.");
            return;
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId, requirement.Permission);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug(
                "Access denied: User {UserId} lacks required permission '{Permission}'.",
                userId,
                requirement.Permission);
        }
    }
}
