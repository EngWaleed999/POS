// Domain error definitions for the Permission entity.

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Domain.Errors;

public static class PermissionErrors
{
    public static readonly Error EmptyResource =
        Error.Validation("Permission.EmptyResource", "Permission resource cannot be empty.");

    public static readonly Error EmptyAction =
        Error.Validation("Permission.EmptyAction", "Permission action cannot be empty.");

    public static readonly Error EmptyScope =
        Error.Validation("Permission.EmptyScope", "Permission scope cannot be empty.");

    public static readonly Error EmptyCategory =
        Error.Validation("Permission.EmptyCategory", "Permission category cannot be empty.");

    public static readonly Error EmptyPermissionId =
        Error.Validation("Permission.EmptyPermissionId", "A valid non-empty permission identifier must be provided.");
}
