// Domain error definitions for the Role entity.

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Domain.Errors;

public static class RoleErrors
{
    public static readonly Error EmptyRoleName =
        Error.Validation("Role.EmptyRoleName", "Role name cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyRoleId =
        Error.Validation("Role.EmptyRoleId", "A valid non-empty role identifier must be provided.");

    public static readonly Error AlreadyActive =
        Error.Conflict("Role.AlreadyActive", "Role is already active.");

    public static readonly Error AlreadyDeactivated =
        Error.Conflict("Role.AlreadyDeactivated", "Role is already deactivated.");
}
