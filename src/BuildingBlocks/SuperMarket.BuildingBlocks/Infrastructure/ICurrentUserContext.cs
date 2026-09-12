namespace SuperMarket.BuildingBlocks.Infrastructure;

/// <summary>
/// Defines the contract for accessing current user execution context (e.g., Keycloak JWT claims).
/// Injected into audit interceptors and domain handlers to identify the actor performing operations.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// The unique identifier of the authenticated user (e.g. Keycloak 'sub' claim).
    /// Returns null when running in background tasks, migrations, or unauthenticated contexts.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Indicates whether the current operation is performed by an authenticated user.
    /// </summary>
    bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
}
