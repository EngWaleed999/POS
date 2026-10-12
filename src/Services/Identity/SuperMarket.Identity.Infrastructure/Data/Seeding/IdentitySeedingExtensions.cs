using Microsoft.Extensions.DependencyInjection;
using SuperMarket.Identity.Application.Abstractions.Persistence;

namespace SuperMarket.Identity.Infrastructure.Data.Seeding;

/// <summary>
/// Host and ServiceProvider extensions for executing identity seeding safely at startup.
/// </summary>
public static class IdentitySeedingExtensions
{
    /// <summary>
    /// Creates an isolated service scope to resolve <see cref="IIdentityDataSeeder"/> and run the idempotent seeding process.
    /// </summary>
    /// <param name="serviceProvider">The application service provider root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SeedIdentityDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await using var scope = serviceProvider.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IIdentityDataSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
