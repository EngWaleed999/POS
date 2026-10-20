namespace SuperMarket.Identity.Application.Abstractions.Authorization;

/// <summary>
/// Port abstraction for checking and managing cached authorization permissions.
/// Enables sub-millisecond Cache-Aside permission evaluation without database roundtrips.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Verifies if a user holds a specific atomic permission (resource:action:scope).
    /// </summary>
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active permissions granted to the specified user.
    /// </summary>
    Task<HashSet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately invalidates cached permissions for a single user (e.g., when role changes).
    /// </summary>
    Task InvalidateUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately invalidates cached permissions for all users assigned to the specified role.
    /// </summary>
    Task InvalidateRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);
}
