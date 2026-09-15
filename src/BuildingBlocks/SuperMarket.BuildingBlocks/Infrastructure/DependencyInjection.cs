using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SuperMarket.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    // -------------------------------------------------------------------------
    // Service Registration (DI Container)
    // -------------------------------------------------------------------------
    public static IServiceCollection AddBuildingBlocksWeb(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    // -------------------------------------------------------------------------
    // Application Pipeline Configuration (Middleware Chain)
    // -------------------------------------------------------------------------
    public static IApplicationBuilder UseBuildingBlocksWeb(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();

        return app;
    }
}
