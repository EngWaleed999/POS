// Domain error definitions for POSRegister aggregate root.

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Domain.Errors;

public static class POSRegisterErrors
{
    public static readonly Error EmptyRegisterCode =
        Error.Validation("POSRegister.EmptyRegisterCode", "Register code cannot be empty or consist solely of whitespace.");

    public static readonly Error InvalidRegisterCodeLength =
        Error.Validation("POSRegister.InvalidRegisterCodeLength", "Register code must be between 2 and 20 characters in length.");

    public static readonly Error EmptyRegisterName =
        Error.Validation("POSRegister.EmptyRegisterName", "Register name cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyBranchId =
        Error.Validation("POSRegister.EmptyBranchId", "A valid non-empty branch identifier must be provided.");

    public static readonly Error AlreadyActive =
        Error.Conflict("POSRegister.AlreadyActive", "Register is already active.");

    public static readonly Error AlreadyDeactivated =
        Error.Conflict("POSRegister.AlreadyDeactivated", "Register is already deactivated.");

    public static readonly Error AlreadyDeleted =
        Error.Conflict("POSRegister.AlreadyDeleted", "Register is already deleted.");

    public static readonly Error DeletedRegisterCannotBeModified =
        Error.Conflict("POSRegister.DeletedRegisterCannotBeModified", "Cannot perform operations on a deleted register.");

    public static readonly Error SameBranchReassignment =
        Error.Conflict("POSRegister.SameBranchReassignment", "Cannot reassign register to the branch it is already assigned to.");
}
