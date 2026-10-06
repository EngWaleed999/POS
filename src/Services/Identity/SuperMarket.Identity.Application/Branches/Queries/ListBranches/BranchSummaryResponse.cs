using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Queries.ListBranches;

/// <summary>
/// Lightweight summary DTO representing a supermarket branch in paginated administrative lists.
/// Implements IResponseDto to satisfy query compile-time architectural constraints.
/// </summary>
public sealed record BranchSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string City,
    string Phone,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt) : IResponseDto;
