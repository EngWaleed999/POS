using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);

            // Register Pipeline Behaviors in deliberate execution sequence:
            // 1. Logging: captures entry, exit, and error state
            // 2. Validation: validates inputs via FluentValidation before executing handler
            // 3. Performance: tracks elapsed latency and logs slow requests (>500ms)
            configuration.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
            configuration.AddOpenBehavior(typeof(PerformancePipelineBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
