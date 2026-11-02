using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace SuperMarket.Identity.API.Authorization;

/// <summary>
/// Extension methods for Minimal APIs to enforce fine-grained atomic permissions.
/// Connects route builders directly with the Dynamic Permission Authorization pipeline.
/// </summary>
public static class EndpointAuthorizationExtensions
{
    /// <summary>
    /// Enforces that the caller has the specified atomic permission (e.g. "users:create:all").
    /// </summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission) =>
        builder.RequireAuthorization($"{HasPermissionAttribute.PolicyPrefix}{permission}");

    /// <summary>
    /// Enforces that all endpoints within the route group require the specified atomic permission.
    /// </summary>
    public static RouteGroupBuilder RequirePermission(this RouteGroupBuilder builder, string permission) =>
        builder.RequireAuthorization($"{HasPermissionAttribute.PolicyPrefix}{permission}");
}
