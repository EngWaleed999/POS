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

    public static readonly Error NotFound =
        Error.NotFound("User.NotFound", "The requested user was not found.");

    public static readonly Error UsernameAlreadyExists =
        Error.Conflict("User.UsernameAlreadyExists", "A user with the specified username already exists.");

    public static readonly Error PhoneNumberAlreadyExists =
        Error.Conflict("User.PhoneNumberAlreadyExists", "A user with the specified phone number already exists.");

    public static readonly Error EmailAlreadyExists =
        Error.Conflict("User.EmailAlreadyExists", "A user with the specified email address already exists.");

    public static readonly Error RoleNotFound =
        Error.NotFound("User.RoleNotFound", "The specified role was not found.");

    public static readonly Error BranchNotFound =
        Error.NotFound("User.BranchNotFound", "The specified branch was not found or is inactive.");

    public static readonly Error InvalidPinFormat =
        Error.Validation("User.InvalidPinFormat", "PIN must be between 4 and 6 numeric digits, and cannot be sequential or repeating.");

    public static readonly Error IdentityProviderFailure =
        Error.Failure("User.IdentityProviderFailure", "External Identity Provider operation failed.");
}
