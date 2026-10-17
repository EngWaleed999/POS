// Minimal API endpoints for supermarket branch lifecycle management.

using MediatR;

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Branches.Commands.ActivateBranch;
using SuperMarket.Identity.Application.Branches.Commands.CreateBranch;
using SuperMarket.Identity.Application.Branches.Commands.DeactivateBranch;
using SuperMarket.Identity.Application.Branches.Commands.DeleteBranch;
using SuperMarket.Identity.Application.Branches.Commands.UpdateBranch;
using SuperMarket.Identity.Application.Branches.Queries.GetBranchById;
using SuperMarket.Identity.Application.Branches.Queries.ListBranches;

namespace SuperMarket.Identity.API.Endpoints;

public static class BranchEndpoints
{
    public static IEndpointRouteBuilder MapBranchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/branches")
            .WithTags("Branches");

        // --- 1. Create Physical Branch ---
        group.MapPost("/", async (CreateBranchRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateBranchCommand(
                Code: request.Code,
                Name: request.Name,
                Street: request.Street,
                City: request.City,
                Region: request.Region,
                PostalCode: request.PostalCode,
                Phone: request.Phone,
                TaxNumber: request.TaxNumber,
                Email: request.Email,
                Currency: request.Currency);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.CreatedAtRoute(
                    routeName: "GetBranchById",
                    routeValues: new { id = result.Value },
                    value: new { id = result.Value })
                : result.ToProblemDetails();
        })
        .WithName("CreateBranch")
        .WithSummary("Creates a new physical supermarket retail branch.")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- 2. List Paginated Branches ---
        group.MapGet("/", async (
            int page = 1,
            int pageSize = 10,
            string? search = null,
            bool? isActive = null,
            ISender sender = default!,
            CancellationToken cancellationToken = default) =>
        {
            var query = new ListBranchesQuery(
                Pagination: new PaginationParams(pageNumber: page, pageSize: pageSize),
                SearchTerm: search,
                IsActive: isActive);

            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("ListBranches")
        .WithSummary("Retrieves a paginated list of retail branches with optional search and active status filters.")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        // --- 3. Get Branch by ID ---
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetBranchByIdQuery(id);
            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetBranchById")
        .WithSummary("Retrieves complete operational details and operating hours for a specific retail branch.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- 4. Update Branch Details ---
        group.MapPut("/{id:guid}", async (Guid id, UpdateBranchRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateBranchCommand(
                BranchId: id,
                Name: request.Name,
                Street: request.Street,
                City: request.City,
                Region: request.Region,
                PostalCode: request.PostalCode,
                Phone: request.Phone,
                TaxNumber: request.TaxNumber,
                Email: request.Email,
                OperatingHours: request.OperatingHours);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("UpdateBranch")
        .WithSummary("Updates operational details and operating hours for an existing retail branch.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- 5. Activate Branch ---
        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new ActivateBranchCommand(id);
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("ActivateBranch")
        .WithSummary("Restores operational status to an active state for a retail branch.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- 6. Deactivate Branch ---
        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new DeactivateBranchCommand(id);
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("DeactivateBranch")
        .WithSummary("Temporarily suspends operational activity for a retail branch.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- 7. Soft Delete Branch ---
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new DeleteBranchCommand(id);
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("DeleteBranch")
        .WithSummary("Soft-deletes a retail branch while preserving all historical and fiscal records.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

// --- Request Contracts ---

public sealed record CreateBranchRequest(
    string Code,
    string Name,
    string Street,
    string City,
    string Region,
    string? PostalCode,
    string Phone,
    string TaxNumber,
    string? Email,
    string Currency = "YE");

public sealed record UpdateBranchRequest(
    string Name,
    string Street,
    string City,
    string Region,
    string? PostalCode,
    string Phone,
    string TaxNumber,
    string? Email,
    IReadOnlyList<UpdateOperatingHoursDto>? OperatingHours = null);
