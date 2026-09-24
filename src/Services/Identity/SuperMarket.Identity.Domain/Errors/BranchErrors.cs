// Domain error definitions for Branch aggregate root.

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Domain.Errors;

public static class BranchErrors
{
    public static readonly Error EmptyBranchCode =
        Error.Validation("Branch.EmptyBranchCode", "Branch code cannot be empty or consist solely of whitespace.");

    public static readonly Error InvalidBranchCodeLength =
        Error.Validation("Branch.InvalidBranchCodeLength", "Branch code must be between 2 and 20 characters in length.");

    public static readonly Error EmptyBranchName =
        Error.Validation("Branch.EmptyBranchName", "Branch name cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyStreet =
        Error.Validation("Branch.EmptyStreet", "Street address cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyCity =
        Error.Validation("Branch.EmptyCity", "City name cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyRegion =
        Error.Validation("Branch.EmptyRegion", "Region name cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyPhone =
        Error.Validation("Branch.EmptyPhone", "Phone number cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyTaxNumber =
        Error.Validation("Branch.EmptyTaxNumber", "Tax/VAT number cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyCurrency =
        Error.Validation("Branch.EmptyCurrency", "Currency cannot be empty or consist solely of whitespace.");

    public static readonly Error AlreadyActive =
        Error.Conflict("Branch.AlreadyActive", "Branch is already active.");

    public static readonly Error AlreadyDeactivated =
        Error.Conflict("Branch.AlreadyDeactivated", "Branch is already deactivated.");

    public static readonly Error AlreadyDeleted =
        Error.Conflict("Branch.AlreadyDeleted", "Branch is already deleted.");

    public static readonly Error DeletedBranchCannotBeModified =
        Error.Conflict("Branch.DeletedBranchCannotBeModified", "Cannot perform operations on a deleted branch.");

    public static readonly Error OperatingHoursNull =
        Error.Validation("Branch.OperatingHoursNull", "Operating hours collection cannot be null.");

    public static readonly Error DuplicateDayOfWeek =
        Error.Conflict("Branch.DuplicateDayOfWeek", "Operating hours cannot contain duplicate entries for the same day of the week.");
}
