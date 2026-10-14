using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Roles.Queries.GetRoleById;

/// <summary>
/// Query to retrieve a single role by its unique identifier along with its assigned permission group packages.
/// Returns RoleDetailResponse upon success or RoleErrors.NotFound on failure.
/// Constrained via IDtoQuery to enforce DTO-only responses.
/// </summary>
public sealed record GetRoleByIdQuery(Guid RoleId) : IDtoQuery<RoleDetailResponse>;
