using Microsoft.EntityFrameworkCore;

namespace SuperMarket.Identity.Infrastructure.Data.Extensions;

public static class IdentityModelBuilderExtensions
{
    /// <summary>
    /// Placeholder for Identity-specific conventions.
    /// Concurrency control (e.g. xmin / RowVersion) is intentionally deferred 
    /// until we execute and observe concurrency failure scenarios in integration testing.
    /// </summary>
    public static void ApplyIdentityPostgresConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        // Deliberately left empty for failure scenario benchmarking (Option B)
    }
}