namespace SuperMarket.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Defines the contract for idempotent seeding of baseline identity data
/// (Permissions, Permission Groups, and Default System Roles).
/// </summary>
public interface IIdentityDataSeeder
{
    /// <summary>
    /// Executes the idempotent seeding process inside an atomic transaction.
    /// Safely inspects current database state and inserts only missing entities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
