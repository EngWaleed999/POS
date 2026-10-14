namespace SuperMarket.Identity.Application.Permissions.Queries.ListPermissionGroups;

/// <summary>
/// Lightweight projection representing an atomic permission summary.
/// </summary>
public sealed record PermissionSummaryResponse(
    Guid Id,
    string Resource,
    string Action,
    string Scope,
    string Category,
    string Key);
