using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SuperMarket.Identity.API.Endpoints;
using SuperMarket.Identity.Application;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Infrastructure;
using SuperMarket.Identity.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

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

// Register Identity Infrastructure (PostgreSQL, Repositories, DbContext, Seeder)
builder.Services.AddIdentityInfrastructure(builder.Configuration);

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

// -----------------------------------------------------------------------------
// 3. Endpoint Mappings (Minimal API Vertical Slices)
// -----------------------------------------------------------------------------
app.MapBranchEndpoints();
app.MapRoleEndpoints();
app.MapPermissionGroupEndpoints();
app.MapStaffEndpoints();
app.MapAuthEndpoints();

app.Run();
