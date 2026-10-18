// Minimal API endpoints for staff and cashier lifecycle management.

using MediatR;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Staff.Commands.ActivateStaff;
using SuperMarket.Identity.Application.Staff.Commands.ChangeStaffRole;
using SuperMarket.Identity.Application.Staff.Commands.DeactivateStaff;
using SuperMarket.Identity.Application.Staff.Commands.RegisterStaff;
using SuperMarket.Identity.Application.Staff.Commands.SetStaffPin;
using SuperMarket.Identity.Application.Staff.Commands.TransferStaff;
using SuperMarket.Identity.Application.Staff.Queries.GetStaffById;
using SuperMarket.Identity.Application.Staff.Queries.ListStaff;

namespace SuperMarket.Identity.API.Endpoints;

public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/staff")
            .WithTags("Staff");

        // --- 1. Register New Staff Member ---
        group.MapPost("/", async (RegisterStaffRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new RegisterStaffCommand(
                Username: request.Username,
                FullName: request.FullName,
                PhoneNumber: request.PhoneNumber,
                Email: request.Email,
                RoleId: request.RoleId,
                BranchId: request.BranchId,
                InitialPassword: request.InitialPassword);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.CreatedAtRoute(
                    routeName: "GetStaffById",
                    routeValues: new { id = result.Value },
                    value: new { id = result.Value })
                : result.ToProblemDetails();
        })
        .WithName("RegisterStaff")
        .WithSummary("Registers a new staff member or cashier across IAM and local domain.")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- 2. List Paginated Staff Members ---
        group.MapGet("/", async (
            int page = 1,
            int pageSize = 10,
            Guid? branchId = null,
            Guid? roleId = null,
            bool? isActive = null,
            string? search = null,
            ISender sender = default!,
            CancellationToken cancellationToken = default) =>
        {
            var query = new ListStaffQuery(
                Pagination: new PaginationParams(pageNumber: page, pageSize: pageSize),
                BranchId: branchId,
                RoleId: roleId,
                IsActive: isActive,
                SearchTerm: search);

            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("ListStaff")
        .WithSummary("Retrieves a paginated list of staff members with branch, role, and search filters.")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        // --- 3. Get Staff Member by ID ---
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetStaffByIdQuery(id);
            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetStaffById")
        .WithSummary("Retrieves complete profile details for a specific staff member.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- 4. Set Staff PIN ---
        group.MapPut("/{id:guid}/pin", async (Guid id, SetStaffPinRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new SetStaffPinCommand(
                UserId: id,
                Pin: request.Pin);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("SetStaffPin")
        .WithSummary("Sets or updates the cashier fast-login PIN secured with PBKDF2.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- 5. Transfer Staff Member to Branch ---
        group.MapPut("/{id:guid}/transfer", async (Guid id, TransferStaffRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new TransferStaffCommand(
                UserId: id,
                NewBranchId: request.NewBranchId);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("TransferStaff")
        .WithSummary("Reassigns a staff member to another branch or headquarters.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- 6. Change Staff Role ---
        group.MapPut("/{id:guid}/role", async (Guid id, ChangeStaffRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new ChangeStaffRoleCommand(
                UserId: id,
                NewRoleId: request.NewRoleId);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("ChangeStaffRole")
        .WithSummary("Updates a staff member's role and synchronizes permissions in Keycloak.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- 7. Activate Staff Member ---
        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new ActivateStaffCommand(id);
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("ActivateStaff")
        .WithSummary("Restores an active account status for a staff member.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- 8. Deactivate Staff Member ---
        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new DeactivateStaffCommand(id);
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("DeactivateStaff")
        .WithSummary("Suspends a staff member account and invalidates Keycloak sessions.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

// --- Request Contracts ---

public sealed record RegisterStaffRequest(
    string Username,
    string FullName,
    string PhoneNumber,
    string? Email,
    Guid RoleId,
    Guid? BranchId,
    string InitialPassword);

public sealed record SetStaffPinRequest(string Pin);

public sealed record TransferStaffRequest(Guid? NewBranchId);

public sealed record ChangeStaffRoleRequest(Guid NewRoleId);
