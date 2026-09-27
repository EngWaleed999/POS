using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SuperMarket.Identity.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core CLI tooling (e.g., migrations generation).
/// Reads connection string strictly from the environment variable 'IDENTITY_DB_CONNECTION'.
/// Never hardcodes sensitive credentials in source code.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string EnvironmentVariableName = "IDENTITY_DB_CONNECTION";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Throw a clear, developer-friendly diagnostic exception explaining how to set the variable for CLI tools
            throw new InvalidOperationException(
                $"Design-time execution failed: Environment variable '{EnvironmentVariableName}' is not set. " +
                $"Please set it before running EF Core CLI migrations. " +
                $"Example: $env:{EnvironmentVariableName}=\"Host=localhost;Port=5432;Database=supermarket_identity;Username=<user>;Password=<pwd>;\"");
        }

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.DefaultSchema);
        })
        .UseSnakeCaseNamingConvention();

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
