namespace SuperMarket.Identity.Domain.Constants;

/// <summary>
/// Canonical atomic permission string constants for the Identity service.
/// Follows the standard resource:action:scope taxonomy defined in IdentitySeedCatalog.
/// </summary>
public static class IdentityPermissions
{
    public static class Users
    {
        public const string Create = "users:create:all";
        public const string Read = "users:read:all";
        public const string Update = "users:update:all";
        public const string Activate = "users:activate:all";
        public const string Deactivate = "users:deactivate:all";
        public const string Delete = "users:delete:all";
        public const string ResetPassword = "users:reset-password:all";
    }

    public static class Branches
    {
        public const string Create = "branches:create:all";
        public const string Read = "branches:read:all";
        public const string Update = "branches:update:all";
        public const string Activate = "branches:activate:all";
        public const string Deactivate = "branches:deactivate:all";
        public const string TransferManager = "branches:transfer-manager:all";
    }

    public static class Roles
    {
        public const string Create = "roles:create:all";
        public const string Read = "roles:read:all";
        public const string Update = "roles:update:all";
        public const string Delete = "roles:delete:all";
    }

    public static class PermissionGroups
    {
        public const string Read = "permission-groups:read:all";
    }
}
