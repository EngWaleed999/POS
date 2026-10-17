using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Application.Permissions.Queries.ListPermissionGroups;

namespace SuperMarket.Identity.API.Endpoints;

/// <summary>
/// Minimal API endpoints for querying permission groups and atomic permissions catalog.
/// </summary>
public static class PermissionGroupEndpoints
{
    public static IEndpointRouteBuilder MapPermissionGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/permission-groups")
            .WithTags("Permission Groups");

        // 1. List All Permission Groups with Constituent Permissions
        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new ListPermissionGroupsQuery();
            var result = await sender.Send(query, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("ListPermissionGroups")
        .WithSummary("Retrieves all packaged permission groups with their atomic constituent permissions.")
        .Produces<IReadOnlyList<PermissionGroupSummaryResponse>>(StatusCodes.Status200OK);

        return app;
    }
}
