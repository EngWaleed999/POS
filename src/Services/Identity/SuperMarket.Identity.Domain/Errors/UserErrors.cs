// Domain error definitions for the User aggregate root.

using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.Identity.Domain.Errors;

public static class UserErrors
{
    public static readonly Error EmptyUsername =
        Error.Validation("User.EmptyUsername", "Username cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyKeycloakUserId =
        Error.Validation("User.EmptyKeycloakUserId", "Keycloak user identifier cannot be empty.");

    public static readonly Error EmptyFullName =
        Error.Validation("User.EmptyFullName", "Full name cannot be empty or consist solely of whitespace.");

    public static readonly Error EmptyRoleId =
        Error.Validation("User.EmptyRoleId", "A valid non-empty role identifier must be provided.");

    public static readonly Error EmptyPhoneNumber =
        Error.Validation("User.EmptyPhoneNumber", "Phone number cannot be empty or consist solely of whitespace.");

    public static readonly Error InvalidEmail =
        Error.Validation("User.InvalidEmail", "The provided email address format is invalid.");

    public static readonly Error EmptyPinHash =
        Error.Validation("User.EmptyPinHash", "PIN hash cannot be empty or whitespace.");

    public static readonly Error AlreadyActive =
        Error.Conflict("User.AlreadyActive", "User account is already active.");

    public static readonly Error AlreadyDeactivated =
        Error.Conflict("User.AlreadyDeactivated", "User account is already deactivated.");

    public static readonly Error AlreadyDeleted =
        Error.Conflict("User.AlreadyDeleted", "User account is already deleted.");

    public static readonly Error DeletedUserCannotBeModified =
        Error.Conflict("User.DeletedUserCannotBeModified", "Cannot perform operations on a deleted user account.");

    public static readonly Error AccountLockedOut =
        Error.Conflict("User.AccountLockedOut", "User account is temporarily locked out due to multiple consecutive failed authentication attempts.");
}
