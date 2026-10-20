using Microsoft.AspNetCore.Authorization;

namespace SuperMarket.Identity.API.Authorization;

/// <summary>
/// Authorization requirement encapsulating an atomic permission check.
/// </summary>
public sealed class PermissionAuthorizationRequirement : IAuthorizationRequirement
{
    public PermissionAuthorizationRequirement(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Permission = permission.Trim().ToLowerInvariant();
    }

    public string Permission { get; }
}
