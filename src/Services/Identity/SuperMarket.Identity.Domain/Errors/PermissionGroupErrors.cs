// Domain error definitions for the PermissionGroup entity.

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Domain.Errors;

public static class PermissionGroupErrors
{
    public static readonly Error EmptyGroupName =
        Error.Validation("PermissionGroup.EmptyGroupName", "Permission group name cannot be empty.");

    public static readonly Error EmptyGroupId =
        Error.Validation("PermissionGroup.EmptyGroupId", "A valid non-empty permission group identifier must be provided.");
}
