using SuperMarket.BuildingBlocks.Application;

namespace SuperMarket.Identity.Application.Branches.Commands.UpdateBranch;

/// <summary>
/// Operational schedule entry for a specific day of the week within an update payload.
/// </summary>
public sealed record UpdateOperatingHoursDto(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsClosed = false);
