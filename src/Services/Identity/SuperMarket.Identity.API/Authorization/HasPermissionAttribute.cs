using Microsoft.AspNetCore.Authorization;

namespace SuperMarket.Identity.API.Authorization;

/// <summary>
/// Declarative attribute for endpoint protection using atomic permissions.
/// Format: [HasPermission("resource:action:scope")] (e.g., [HasPermission("roles:read:system")]).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public HasPermissionAttribute(string permission)
        : base(policy: $"{PolicyPrefix}{permission}")
    {
        Permission = permission;
    }

    public string Permission { get; }
}
