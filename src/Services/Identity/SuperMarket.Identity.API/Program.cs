using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using SuperMarket.Identity.API.Authorization;
using SuperMarket.Identity.API.Endpoints;
using SuperMarket.Identity.Application;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Infrastructure;
using SuperMarket.Identity.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// Structured Logging (Serilog: Console for Cloud/Production, Rolling File for Dev)
// -----------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "SuperMarket.Identity.API")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}");

    // Local Development: Write rolling daily log files inside logs/ folder (Git-ignored)
    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.File(
            path: "logs/identity-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}");
    }
}, writeToProviders: true);

// -----------------------------------------------------------------------------
// 1. Service Registrations (Dependency Injection)
// -----------------------------------------------------------------------------
builder.Services.AddProblemDetails();
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

// Register Identity Application (MediatR, Pipeline Behaviors, FluentValidation)
builder.Services.AddIdentityApplication();

// Register Identity Infrastructure (PostgreSQL, Repositories, DbContext, Seeder, Redis)
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// Register Keycloak JWT Bearer Authentication (ADR-004: In-Memory JWKS Caching)
var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"] ?? "http://localhost:8080";
var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "supermarket";
var authority = $"{keycloakBaseUrl.TrimEnd('/')}/realms/{keycloakRealm}";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authority;
    options.RequireHttpsMetadata = false; // Dev/Docker compatibility; enforce true in production with public SSL
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = authority,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "preferred_username",
        RoleClaimType = "roles"
    };
});

// Register Dynamic Permission Authorization (Cache-Aside Redis)
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// OpenTelemetry Observability (Traces -> Jaeger, Metrics -> Prometheus via OTel Collector, Logs -> Loki via OTel Collector)
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "SuperMarket.Identity.API", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
            })
            .AddSource("Npgsql")
            .AddSource("SuperMarket.Identity.Redis")
            .AddSource("OpenTelemetry.Instrumentation.StackExchangeRedis")
            .AddSource("SuperMarket.POS.Core")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("SuperMarket.POS.Core")
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddMeter("System.Net.Http")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
    })
    .WithLogging(logging =>
    {
        logging.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        });
    });

// OpenAPI & Scalar Documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// -----------------------------------------------------------------------------
// 2. HTTP Request Pipeline & Middleware
// -----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "SuperMarket Identity API";
        options.Theme = ScalarTheme.Purple;
    });

    // Automatically apply pending migrations and run baseline seeding in Development
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<IIdentityDataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to apply automatic migrations or data seeding at startup. Verify database connectivity.");
    }
}

app.UseHttpsRedirection();

// Serilog HTTP Request Logging (Standardized, structured HTTP metrics)
app.UseSerilogRequestLogging();

// Expose W3C TraceId in HTTP response headers for developer visibility and client correlation
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            context.Response.Headers.TryAdd("X-Trace-Id", traceId);
        }
        return Task.CompletedTask;
    });

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// -----------------------------------------------------------------------------
// 3. Endpoint Mappings (Minimal API Vertical Slices)
// -----------------------------------------------------------------------------
app.MapBranchEndpoints();
app.MapRoleEndpoints();
app.MapPermissionGroupEndpoints();
app.MapStaffEndpoints();
app.MapAuthEndpoints();

app.Run();
