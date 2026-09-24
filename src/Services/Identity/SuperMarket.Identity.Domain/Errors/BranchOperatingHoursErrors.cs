// Domain error definitions for BranchOperatingHours entity.

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Domain.Errors;

public static class BranchOperatingHoursErrors
{
    public static readonly Error EmptyBranchId =
        Error.Validation("BranchOperatingHours.EmptyBranchId", "A valid non-empty branch identifier must be provided.");

    public static readonly Error SameOpenAndCloseTime =
        Error.Validation("BranchOperatingHours.SameOpenAndCloseTime", "Open time and close time cannot be identical for an open branch day.");

    public static readonly Error DayNotFound =
        Error.NotFound("BranchOperatingHours.DayNotFound", "Operating hours for the specified day of week were not found.");
}
