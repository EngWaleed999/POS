using MediatR;

using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Roles.Commands.CreateRole;
using SuperMarket.Identity.Application.Roles.Commands.UpdateRolePermissionGroups;
using SuperMarket.Identity.Application.Roles.Queries.GetRoleById;
using SuperMarket.Identity.Application.Roles.Queries.ListRoles;

namespace SuperMarket.Identity.API.Endpoints;

/// <summary>
/// Minimal API endpoints for dynamic role lifecycle management.
/// Maps HTTP transport requests directly to MediatR CQRS commands and queries.
/// </summary>
public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/roles")
            .WithTags("Roles");

        // 1. Create Dynamic Role
        group.MapPost("/", async (CreateRoleRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateRoleCommand(
                RoleName: request.RoleName,
                Description: request.Description);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.CreatedAtRoute(
                    routeName: "GetRoleById",
                    routeValues: new { id = result.Value },
                    value: new { id = result.Value })
                : result.ToProblemDetails();
        })
        .WithName("CreateRole")
        .WithSummary("Creates a new dynamic system or branch role.")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 2. List Paginated Roles with Optional Filtering
        group.MapGet("/", async (
            int page = 1,
            int pageSize = 10,
            string? search = null,
            bool? isActive = null,
            ISender sender = default!,
            CancellationToken cancellationToken = default) =>
        {
            var query = new ListRolesQuery(
                Pagination: new PaginationParams(pageNumber: page, pageSize: pageSize),
                SearchTerm: search,
                IsActive: isActive);

            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("ListRoles")
        .WithSummary("Retrieves offset-paginated dynamic system roles with search and active status filters.")
        .Produces<PagedList<RoleSummaryResponse>>(StatusCodes.Status200OK);

        // 3. Get Role Details By ID
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetRoleByIdQuery(RoleId: id);
            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetRoleById")
        .WithSummary("Retrieves detailed role information including assigned permission groups.")
        .Produces<RoleDetailResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 4. Full State Synchronization for Assigned Permission Groups (ADR-004 & ADR-005)
        group.MapPut("/{id:guid}/permission-groups", async (
            Guid id,
            UpdateRolePermissionGroupsRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateRolePermissionGroupsCommand(
                RoleId: id,
                PermissionGroupIds: request.PermissionGroupIds ?? []);

            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        })
        .WithName("UpdateRolePermissionGroups")
        .WithSummary("Synchronizes the assigned permission group packages for a role using in-memory state-diffing.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

/// <summary>
/// HTTP request contract for creating a dynamic role.
/// </summary>
public sealed record CreateRoleRequest(
    string RoleName,
    string Description
    );

/// <summary>
/// HTTP request contract for synchronizing permission groups assigned to a role.
/// </summary>
public sealed record UpdateRolePermissionGroupsRequest(
    IReadOnlyList<Guid>? PermissionGroupIds);
