using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Queries.GetBranchById;

/// <summary>
/// Detailed response payload representing a supermarket branch and its operational configuration.
/// Implements IResponseDto to satisfy query compile-time architectural constraints.
/// </summary>
public sealed record BranchDetailResponse(
    Guid Id,
    string Code,
    string Name,
    string Street,
    string City,
    string Region,
    string? PostalCode,
    string Phone,
    string TaxNumber,
    string? Email,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BranchOperatingHoursResponse> OperatingHours) : IResponseDto;

/// <summary>
/// Operational schedule item for a single day of the week.
/// </summary>
public sealed record BranchOperatingHoursResponse(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsClosed,
    bool IsOvernight) : IResponseDto;
