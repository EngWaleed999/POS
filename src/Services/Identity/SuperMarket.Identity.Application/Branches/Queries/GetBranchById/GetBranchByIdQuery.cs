using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Queries.GetBranchById;

/// <summary>
/// Query to retrieve a single branch by its unique identifier with full operational details.
/// Returns BranchDetailResponse upon success or BranchErrors.NotFound on failure.
/// Constrained via IDtoQuery to enforce that only DTOs can be returned.
/// </summary>
public sealed record GetBranchByIdQuery(Guid BranchId) : IDtoQuery<BranchDetailResponse>;
