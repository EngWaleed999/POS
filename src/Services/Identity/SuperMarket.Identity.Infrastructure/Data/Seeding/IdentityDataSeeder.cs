using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SuperMarket.Identity.Application.Abstractions.Authentication;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Infrastructure.Data.Seeding;

/// <summary>
/// Production-grade, idempotent database seeder for baseline identity entities.
/// Seeds Permissions, Permission Groups, Permission Group Links, Default Roles,
/// and Role-Group mappings within an explicit, atomic database transaction.
/// </summary>
public sealed class IdentityDataSeeder : IIdentityDataSeeder
{
    private readonly IdentityDbContext _dbContext;
    private readonly IIdentityProviderService _identityProviderService;
    private readonly ILogger<IdentityDataSeeder> _logger;

    public IdentityDataSeeder(
        IdentityDbContext dbContext,
        IIdentityProviderService identityProviderService,
        ILogger<IdentityDataSeeder> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityProviderService = identityProviderService ?? throw new ArgumentNullException(nameof(identityProviderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        ValidateCatalogIntegrity();

        _logger.LogInformation("Starting Identity database seeding...");

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            try
            {
                // -----------------------------------------------------------------
                // 1. Seed Atomic Permissions (State-diffed by resource:action:scope)
                // -----------------------------------------------------------------
                var existingPermissionsRaw = await _dbContext.Permissions
                    .Select(p => new { p.Id, p.Resource, p.Action, p.Scope })
                    .ToListAsync(ct);

                var permissionLookup = existingPermissionsRaw
                    .ToDictionary(
                        p => $"{p.Resource.ToLowerInvariant()}:{p.Action.ToLowerInvariant()}:{p.Scope.ToLowerInvariant()}",
                        p => p.Id,
                        StringComparer.OrdinalIgnoreCase);

                var missingPermissions = new List<Permission>();
                foreach (var def in IdentitySeedCatalog.Permissions)
                {
                    if (!permissionLookup.ContainsKey(def.Key))
                    {
                        var permissionResult = Permission.Create(def.Resource, def.Action, def.Scope, def.Category);
                        if (permissionResult.IsFailure)
                        {
                            throw new InvalidOperationException(
                                $"Failed to create permission '{def.Key}': {permissionResult.Error.Description}");
                        }

                        missingPermissions.Add(permissionResult.Value);
                        permissionLookup[def.Key] = permissionResult.Value.Id;
                    }
                }

                if (missingPermissions.Count > 0)
                {
                    _dbContext.Permissions.AddRange(missingPermissions);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Seeded {Count} new permissions.", missingPermissions.Count);
                }
                else
                {
                    _logger.LogInformation("All permissions already exist. Skipped.");
                }

                // -----------------------------------------------------------------
                // 2. Seed Permission Groups (State-diffed by GroupName)
                // -----------------------------------------------------------------
                var existingGroupsRaw = await _dbContext.PermissionGroups
                    .Select(g => new { g.Id, g.GroupName })
                    .ToListAsync(ct);

                var groupLookup = existingGroupsRaw
                    .ToDictionary(g => g.GroupName, g => g.Id, StringComparer.OrdinalIgnoreCase);

                var missingGroups = new List<PermissionGroup>();
                foreach (var groupDef in IdentitySeedCatalog.PermissionGroups)
                {
                    if (!groupLookup.ContainsKey(groupDef.GroupName))
                    {
                        var groupResult = PermissionGroup.Create(groupDef.GroupName, groupDef.Description);
                        if (groupResult.IsFailure)
                        {
                            throw new InvalidOperationException(
                                $"Failed to create permission group '{groupDef.GroupName}': {groupResult.Error.Description}");
                        }

                        missingGroups.Add(groupResult.Value);
                        groupLookup[groupDef.GroupName] = groupResult.Value.Id;
                    }
                }

                if (missingGroups.Count > 0)
                {
                    _dbContext.PermissionGroups.AddRange(missingGroups);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Seeded {Count} new permission groups.", missingGroups.Count);
                }
                else
                {
                    _logger.LogInformation("All permission groups already exist. Skipped.");
                }

                // -----------------------------------------------------------------
                // 3. Link Permissions to Groups (State-diffed by composite key)
                // -----------------------------------------------------------------
                var existingGroupItems = (await _dbContext.PermissionGroupItems
                    .Select(x => new { x.GroupId, x.PermissionId })
                    .ToListAsync(ct))
                    .Select(x => (x.GroupId, x.PermissionId))
                    .ToHashSet();

                var missingGroupItems = new List<PermissionGroupItem>();
                foreach (var groupDef in IdentitySeedCatalog.PermissionGroups)
                {
                    if (!groupLookup.TryGetValue(groupDef.GroupName, out var groupId))
                        continue;

                    foreach (var permKey in groupDef.PermissionKeys)
                    {
                        if (permissionLookup.TryGetValue(permKey, out var permissionId))
                        {
                            if (!existingGroupItems.Contains((groupId, permissionId)))
                            {
                                var itemResult = PermissionGroupItem.Create(groupId, permissionId);
                                if (itemResult.IsFailure)
                                {
                                    throw new InvalidOperationException(
                                        $"Failed to link group '{groupDef.GroupName}' to permission '{permKey}': {itemResult.Error.Description}");
                                }

                                missingGroupItems.Add(itemResult.Value);
                                existingGroupItems.Add((groupId, permissionId));
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Seed catalog permission key '{Key}' not found for group '{Group}'.", permKey, groupDef.GroupName);
                        }
                    }
                }

                if (missingGroupItems.Count > 0)
                {
                    _dbContext.PermissionGroupItems.AddRange(missingGroupItems);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Linked {Count} new group-permission items.", missingGroupItems.Count);
                }
                else
                {
                    _logger.LogInformation("All group-permission items already linked. Skipped.");
                }

                // -----------------------------------------------------------------
                // 4. Seed Default System Roles (State-diffed by RoleName)
                // -----------------------------------------------------------------
                var existingRolesRaw = await _dbContext.Roles
                    .Select(r => new { r.Id, r.RoleName })
                    .ToListAsync(ct);

                var roleLookup = existingRolesRaw
                    .ToDictionary(r => r.RoleName, r => r.Id, StringComparer.OrdinalIgnoreCase);

                var missingRoles = new List<Role>();
                foreach (var roleDef in IdentitySeedCatalog.Roles)
                {
                    if (!roleLookup.ContainsKey(roleDef.RoleName))
                    {
                        var roleResult = Role.Create(roleDef.RoleName, roleDef.Description);
                        if (roleResult.IsFailure)
                        {
                            throw new InvalidOperationException(
                                $"Failed to create role '{roleDef.RoleName}': {roleResult.Error.Description}");
                        }

                        missingRoles.Add(roleResult.Value);
                        roleLookup[roleDef.RoleName] = roleResult.Value.Id;
                    }
                }

                if (missingRoles.Count > 0)
                {
                    _dbContext.Roles.AddRange(missingRoles);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Seeded {Count} new system roles.", missingRoles.Count);
                }
                else
                {
                    _logger.LogInformation("All system roles already exist. Skipped.");
                }

                // -----------------------------------------------------------------
                // 5. Link Roles to Permission Groups (State-diffed by composite key)
                // -----------------------------------------------------------------
                var existingRoleGroups = (await _dbContext.RolePermissionGroups
                    .Select(x => new { x.RoleId, x.GroupId })
                    .ToListAsync(ct))
                    .Select(x => (x.RoleId, x.GroupId))
                    .ToHashSet();

                var missingRoleGroups = new List<RolePermissionGroup>();
                foreach (var roleDef in IdentitySeedCatalog.Roles)
                {
                    if (!roleLookup.TryGetValue(roleDef.RoleName, out var roleId))
                        continue;

                    foreach (var groupName in roleDef.GroupNames)
                    {
                        if (groupLookup.TryGetValue(groupName, out var groupId))
                        {
                            if (!existingRoleGroups.Contains((roleId, groupId)))
                            {
                                var linkResult = RolePermissionGroup.Create(roleId, groupId);
                                if (linkResult.IsFailure)
                                {
                                    throw new InvalidOperationException(
                                        $"Failed to link role '{roleDef.RoleName}' to group '{groupName}': {linkResult.Error.Description}");
                                }

                                missingRoleGroups.Add(linkResult.Value);
                                existingRoleGroups.Add((roleId, groupId));
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Seed catalog group '{Group}' not found for role '{Role}'.", groupName, roleDef.RoleName);
                        }
                    }
                }

                if (missingRoleGroups.Count > 0)
                {
                    _dbContext.RolePermissionGroups.AddRange(missingRoleGroups);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Linked {Count} new role-permission-group items.", missingRoleGroups.Count);
                }
                else
                {
                    _logger.LogInformation("All role-permission-group items already linked. Skipped.");
                }

                // -----------------------------------------------------------------
                // 6. Seed Default SystemAdmin User (Linked to Keycloak)
                // -----------------------------------------------------------------
                if (roleLookup.TryGetValue("System Admin", out var adminRoleId))
                {
                    var existingAdmin = await _dbContext.Users
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(u => u.Username == "admin", ct);

                    if (existingAdmin is null)
                    {
                        string keycloakUserId;
                        try
                        {
                            var idpCreateResult = await _identityProviderService.CreateUserAsync(
                                username: "admin",
                                email: "admin@supermarket.pos",
                                phoneNumber: "+967770000000",
                                fullName: "System Administrator",
                                initialPassword: "admin123",
                                cancellationToken: ct);

                            if (idpCreateResult.IsSuccess)
                            {
                                keycloakUserId = idpCreateResult.Value;
                                _logger.LogInformation("Keycloak user 'admin' created with ID {UserId}", keycloakUserId);
                            }
                            else
                            {
                                var getExistingResult = await _identityProviderService.GetUserIdByUsernameAsync("admin", ct);
                                if (getExistingResult.IsSuccess)
                                {
                                    keycloakUserId = getExistingResult.Value;
                                    _logger.LogInformation("Found existing Keycloak user 'admin' with ID {UserId}", keycloakUserId);
                                }
                                else
                                {
                                    keycloakUserId = Guid.NewGuid().ToString();
                                    _logger.LogWarning("Keycloak user creation returned failure ({Error}). Using fallback ID.", idpCreateResult.Error.Description);
                                }
                            }

                            // Assign System Admin role in Keycloak
                            var roleAssignResult = await _identityProviderService.AssignRoleAsync(keycloakUserId, "System Admin", ct);
                            if (roleAssignResult.IsFailure)
                            {
                                _logger.LogWarning("Could not assign 'System Admin' role in Keycloak: {Error}", roleAssignResult.Error.Description);
                            }
                        }
                        catch (Exception ex)
                        {
                            keycloakUserId = Guid.NewGuid().ToString();
                            _logger.LogWarning(ex, "Keycloak service unavailable during seeding. Default admin seeded with fallback ID.");
                        }

                        var adminUserResult = User.Create(
                            username: "admin",
                            phoneNumber: "+967770000000",
                            keycloakUserId: keycloakUserId,
                            fullName: "System Administrator",
                            roleId: adminRoleId,
                            email: "admin@supermarket.pos",
                            branchId: null);

                        if (adminUserResult.IsSuccess)
                        {
                            _dbContext.Users.Add(adminUserResult.Value);
                            await _dbContext.SaveChangesAsync(ct);
                            _logger.LogInformation("Seeded baseline SystemAdmin user (admin / admin123).");
                        }
                        else
                        {
                            _logger.LogError("Failed to instantiate SystemAdmin user entity: {Error}", adminUserResult.Error.Description);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("SystemAdmin user already exists. Skipped.");
                    }
                }

                // Commit atomic transaction
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Identity database seeding transaction committed successfully. Summary: [{PermCount} Perms, {GroupCount} Groups, {ItemCount} GroupItems, {RoleCount} Roles, {LinkCount} RoleGroups].",
                    missingPermissions.Count,
                    missingGroups.Count,
                    missingGroupItems.Count,
                    missingRoles.Count,
                    missingRoleGroups.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _dbContext.ChangeTracker.Clear();
                _logger.LogCritical(ex, "Critical failure during Identity database seeding. Transaction was fully rolled back to preserve database integrity.");
                throw;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Enforces compile-time/startup integrity of the catalog.
    /// Fails immediately (Fail-Fast) if there are any typos or unresolvable references
    /// between permission groups and atomic permissions, or roles and groups.
    /// </summary>
    private static void ValidateCatalogIntegrity()
    {
        var allPermissionKeys = IdentitySeedCatalog.Permissions
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencedPermissionKeys = IdentitySeedCatalog.PermissionGroups
            .SelectMany(g => g.PermissionKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var orphanedPermissionKeys = referencedPermissionKeys
            .Where(k => !allPermissionKeys.Contains(k))
            .ToList();

        if (orphanedPermissionKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Seed catalog integrity violation: The following permission keys are referenced in groups " +
                $"but do not exist in the permissions catalog: [{string.Join(", ", orphanedPermissionKeys)}]");
        }

        var allGroupNames = IdentitySeedCatalog.PermissionGroups
            .Select(g => g.GroupName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencedGroupNames = IdentitySeedCatalog.Roles
            .SelectMany(r => r.GroupNames)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var orphanedGroups = referencedGroupNames
            .Where(g => !allGroupNames.Contains(g))
            .ToList();

        if (orphanedGroups.Count > 0)
        {
            throw new InvalidOperationException(
                $"Seed catalog integrity violation: The following group names are referenced in roles " +
                $"but do not exist in the groups catalog: [{string.Join(", ", orphanedGroups)}]");
        }
    }
}
