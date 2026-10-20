using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SuperMarket.BuildingBlocks.Infrastructure;
using SuperMarket.Identity.Application.Abstractions.Authorization;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Infrastructure.Caching;
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

        // Register Cryptography & Identity Provider Services
        services.AddSingleton<SuperMarket.Identity.Application.Abstractions.Cryptography.IPinHasher, SuperMarket.Identity.Infrastructure.Cryptography.Pbkdf2PinHasher>();

        // Register Redis Connection Multiplexer (AbortOnConnectFail = false ensures app starts gracefully even if Redis is down)
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 3000;
            options.SyncTimeout = 3000;
            return ConnectionMultiplexer.Connect(options);
        });

        // Register Permission Cache Service
        services.AddScoped<IPermissionService, RedisPermissionService>();

        // Register In-Memory Caching & Keycloak Configuration
        services.AddMemoryCache();
        services.Configure<SuperMarket.Identity.Infrastructure.Authentication.KeycloakOptions>(
            configuration.GetSection(SuperMarket.Identity.Infrastructure.Authentication.KeycloakOptions.SectionName));

        // Register Keycloak REST Service via Typed HttpClient
        services.AddHttpClient<SuperMarket.Identity.Application.Abstractions.Authentication.IIdentityProviderService, 
            SuperMarket.Identity.Infrastructure.Authentication.KeycloakIdentityProviderService>();

        // Register Database Seeding
        services.AddScoped<IIdentityDataSeeder, SuperMarket.Identity.Infrastructure.Data.Seeding.IdentityDataSeeder>();

        return services;
    }
}
