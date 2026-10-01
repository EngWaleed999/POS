using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SuperMarket.Identity.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core CLI tooling (e.g., migrations generation).
/// Follows Microsoft architectural recommendations for design-time configuration:
/// 1. Inspects explicit system environment variables (CI/CD and production environments).
/// 2. Automatically falls back to inspecting local '.env' files in the project hierarchy for DevEx.
/// 3. Never hardcodes secrets or passwords into source code.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string EnvironmentVariableName = "IDENTITY_DB_CONNECTION";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName)
            ?? TryReadConnectionStringFromDotEnv();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Design-time execution failed: Connection string could not be resolved. " +
                $"Please ensure that either the environment variable '{EnvironmentVariableName}' is set, " +
                $"or a local '.env' file exists containing '{EnvironmentVariableName}=...'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.DefaultSchema);
        })
        .UseSnakeCaseNamingConvention();

        return new IdentityDbContext(optionsBuilder.Options);
    }

    private static string? TryReadConnectionStringFromDotEnv()
    {
        try
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (directory is not null)
            {
                // 1. Check direct .env in current directory or parent
                var envPath = Path.Combine(directory.FullName, ".env");
                if (File.Exists(envPath))
                {
                    var found = ParseDotEnvFile(envPath);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                // 2. Check deploy/docker/.env from root
                var dockerEnvPath = Path.Combine(directory.FullName, "deploy", "docker", ".env");
                if (File.Exists(dockerEnvPath))
                {
                    var found = ParseDotEnvFile(dockerEnvPath);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                directory = directory.Parent;
            }
        }
        catch
        {
            // Fall through gracefully if file access is restricted
        }

        return null;
    }

    private static string? ParseDotEnvFile(string filePath)
    {
        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            if (key.Equals(EnvironmentVariableName, StringComparison.OrdinalIgnoreCase))
            {
                // Strip surrounding quotes if present
                if ((value.StartsWith('"') && value.EndsWith('"')) ||
                    (value.StartsWith('\'') && value.EndsWith('\'')))
                {
                    value = value[1..^1];
                }

                return value;
            }
        }

        return null;
    }
}
