using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperMarket.BuildingBlocks.Infrastructure;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Infrastructure.Data;
using SuperMarket.Identity.Infrastructure.Persistence.Repositories;

namespace SuperMarket.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found in configuration.");

        // Register EF Core Interceptors
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        // Register Identity DbContext (Primary data access boundary)
        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.DefaultSchema);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            })
            .UseSnakeCaseNamingConvention();

            options.AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>());
        });

        // Register CQRS Persistence Ports (Scoped to share the exact same DbContext instance per request)
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IIdentityReadDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        // Register Write Repositories
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPOSRegisterRepository, POSRegisterRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionGroupRepository, PermissionGroupRepository>();

        return services;
    }
}
