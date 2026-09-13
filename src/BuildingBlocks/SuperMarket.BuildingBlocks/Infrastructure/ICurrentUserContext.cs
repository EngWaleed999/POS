namespace SuperMarket.BuildingBlocks.Infrastructure;

/// <summary>
/// Defines the contract for accessing current user execution context (e.g., Keycloak JWT claims).
/// Injected into audit interceptors and domain handlers to identify the actor performing operations.
/// </summary>
public interface ICurrentUserContext
{
    
    string? UserId { get; }

  
    bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);
}
