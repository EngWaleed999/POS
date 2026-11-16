using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SuperMarket.Identity.Application.Abstractions.Authorization;
using SuperMarket.Identity.Application.Abstractions.Persistence;

namespace SuperMarket.Identity.Infrastructure.Caching;

/// <summary>
/// Production-grade implementation of IPermissionService using Redis Sets with Cache-Aside strategy.
/// Formats keys as pos:perm:usr:{userId} and enforces graceful degradation if Redis is unavailable.
/// </summary>
public sealed class RedisPermissionService : IPermissionService
{
    public static readonly ActivitySource ActivitySource = new("SuperMarket.Identity.Redis", "1.0.0");
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DisabledTtl = TimeSpan.FromHours(1);
    private const string KeyPrefix = "pos:perm:usr:";
    private const string DisabledKeyPrefix = "pos:usr:disabled:";

    private readonly IConnectionMultiplexer? _redis;
    private readonly IIdentityReadDbContext _readDb;
    private readonly ILogger<RedisPermissionService> _logger;

    public RedisPermissionService(
        IIdentityReadDbContext readDb,
        ILogger<RedisPermissionService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _readDb = readDb;
        _logger = logger;
        _redis = redis;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var normalizedPermission = permission.Trim().ToLowerInvariant();
        var cacheKey = GetCacheKey(userId);
        var disabledKey = GetDisabledKey(userId);
        var db = GetDatabase();

        // 1. Check Redis Cache (Hit Path)
        if (db is not null)
        {
            using var activity = ActivitySource.StartActivity("redis.has_permission");
            activity?.SetTag("db.system", "redis");
            activity?.SetTag("db.operation", "SetContains");
            activity?.SetTag("db.redis.key", cacheKey);

            try
            {
                // Immediate short-circuit: Verify if user has an active disabled tombstone
                if (await db.KeyExistsAsync(disabledKey))
                {
                    activity?.SetTag("pos.user.disabled", true);
                    activity?.SetTag("pos.cache.hit", true);
                    return false;
                }

                if (await db.KeyExistsAsync(cacheKey))
                {
                    var hasPerm = await db.SetContainsAsync(cacheKey, normalizedPermission);
                    activity?.SetTag("pos.cache.hit", true);
                    return hasPerm;
                }

                activity?.SetTag("pos.cache.hit", false);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "Redis error while reading cache key {Key}. Falling back to database.", cacheKey);
            }
        }

        // 2. Cache Miss: Execute optimized LINQ Projection from PostgreSQL
        var permissions = await FetchPermissionsFromDatabaseAsync(userId, cancellationToken);

        // 3. Populate Redis Cache
        if (db is not null && permissions.Count > 0)
        {
            using var writeActivity = ActivitySource.StartActivity("redis.populate_cache");
            writeActivity?.SetTag("db.system", "redis");
            writeActivity?.SetTag("db.operation", "SetAdd");
            writeActivity?.SetTag("db.redis.key", cacheKey);

            try
            {
                var redisValues = permissions.Select(p => (RedisValue)p).ToArray();
                var batch = db.CreateBatch();
                _ = batch.SetAddAsync(cacheKey, redisValues);
                _ = batch.KeyExpireAsync(cacheKey, DefaultTtl);
                batch.Execute();
            }
            catch (Exception ex)
            {
                writeActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "Failed to write permissions to Redis key {Key}.", cacheKey);
            }
        }

        return permissions.Contains(normalizedPermission);
    }

    public async Task<HashSet<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var cacheKey = GetCacheKey(userId);
        var disabledKey = GetDisabledKey(userId);
        var db = GetDatabase();

        // 1. Check Redis Cache
        if (db is not null)
        {
            using var activity = ActivitySource.StartActivity("redis.get_permissions");
            activity?.SetTag("db.system", "redis");
            activity?.SetTag("db.operation", "SetMembers");
            activity?.SetTag("db.redis.key", cacheKey);

            try
            {
                // Immediate short-circuit: Verify if user has an active disabled tombstone
                if (await db.KeyExistsAsync(disabledKey))
                {
                    activity?.SetTag("pos.user.disabled", true);
                    activity?.SetTag("pos.cache.hit", true);
                    return [];
                }

                if (await db.KeyExistsAsync(cacheKey))
                {
                    var members = await db.SetMembersAsync(cacheKey);
                    activity?.SetTag("pos.cache.hit", true);
                    return members.Select(m => m.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }

                activity?.SetTag("pos.cache.hit", false);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "Redis error while reading cache key {Key}. Falling back to database.", cacheKey);
            }
        }

        // 2. Cache Miss: Query Database
        var permissions = await FetchPermissionsFromDatabaseAsync(userId, cancellationToken);

        // 3. Populate Redis Cache
        if (db is not null && permissions.Count > 0)
        {
            using var writeActivity = ActivitySource.StartActivity("redis.populate_cache");
            writeActivity?.SetTag("db.system", "redis");
            writeActivity?.SetTag("db.operation", "SetAdd");
            writeActivity?.SetTag("db.redis.key", cacheKey);

            try
            {
                var redisValues = permissions.Select(p => (RedisValue)p).ToArray();
                var batch = db.CreateBatch();
                _ = batch.SetAddAsync(cacheKey, redisValues);
                _ = batch.KeyExpireAsync(cacheKey, DefaultTtl);
                batch.Execute();
            }
            catch (Exception ex)
            {
                writeActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "Failed to write permissions to Redis key {Key}.", cacheKey);
            }
        }

        return permissions;
    }

    public async Task InvalidateUserPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db is null) return;

        var cacheKey = GetCacheKey(userId);
        using var activity = ActivitySource.StartActivity("redis.invalidate_user");
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "KeyDelete");
        activity?.SetTag("db.redis.key", cacheKey);

        try
        {
            await db.KeyDeleteAsync(cacheKey);
            _logger.LogInformation("Invalidated cached permissions for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to invalidate Redis cache key {Key}", cacheKey);
        }
    }

    public async Task RevokeUserAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db is null) return;

        var cacheKey = GetCacheKey(userId);
        var disabledKey = GetDisabledKey(userId);

        using var activity = ActivitySource.StartActivity("redis.revoke_user");
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "RevokeUserAccess");
        activity?.SetTag("db.redis.user_id", userId.ToString());

        try
        {
            var batch = db.CreateBatch();
            _ = batch.KeyDeleteAsync(cacheKey);
            _ = batch.StringSetAsync(disabledKey, "1", DisabledTtl);
            batch.Execute();

            _logger.LogInformation("Revoked permissions and wrote disabled tombstone in Redis for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to revoke Redis access for user {UserId}", userId);
        }
    }

    public async Task UnmarkUserDisabledAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db is null) return;

        var cacheKey = GetCacheKey(userId);
        var disabledKey = GetDisabledKey(userId);

        using var activity = ActivitySource.StartActivity("redis.unmark_disabled");
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "UnmarkUserDisabled");
        activity?.SetTag("db.redis.user_id", userId.ToString());

        try
        {
            var batch = db.CreateBatch();
            _ = batch.KeyDeleteAsync(disabledKey);
            _ = batch.KeyDeleteAsync(cacheKey);
            batch.Execute();

            _logger.LogInformation("Removed disabled tombstone and cleared permission cache in Redis for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to unmark disabled status in Redis for user {UserId}", userId);
        }
    }

    public async Task InvalidateRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db is null) return;

        using var activity = ActivitySource.StartActivity("redis.invalidate_role");
        activity?.SetTag("db.system", "redis");
        activity?.SetTag("db.operation", "KeyDeleteBatch");
        activity?.SetTag("db.redis.role_id", roleId.ToString());

        try
        {
            // Retrieve all user IDs assigned to this role
            var userIds = await _readDb.Users
                .AsNoTracking()
                .Where(u => u.RoleId == roleId && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            if (userIds.Count == 0) return;

            var keys = userIds.Select(id => (RedisKey)GetCacheKey(id)).ToArray();
            await db.KeyDeleteAsync(keys);

            _logger.LogInformation("Invalidated cached permissions for {Count} users of role {RoleId}", keys.Length, roleId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Failed to invalidate role permissions for role {RoleId}", roleId);
        }
    }

    private static string GetCacheKey(Guid userId) => $"{KeyPrefix}{userId:N}";
    private static string GetDisabledKey(Guid userId) => $"{DisabledKeyPrefix}{userId:N}";

    private IDatabase? GetDatabase()
    {
        try
        {
            if (_redis is not null && _redis.IsConnected)
            {
                return _redis.GetDatabase();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to acquire Redis database instance.");
        }

        return null;
    }

    private async Task<HashSet<string>> FetchPermissionsFromDatabaseAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userState = await _readDb.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new { u.IsActive, u.RoleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (userState is null || !userState.IsActive)
        {
            // Defensive tombstone: Inactive user hitting PostgreSQL on cache miss is cached as disabled
            // to prevent Cache Penetration DoS on the database.
            var db = GetDatabase();
            if (db is not null && userState is not null && !userState.IsActive)
            {
                try
                {
                    await db.StringSetAsync(GetDisabledKey(userId), "1", DisabledTtl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to write disabled tombstone for inactive user {UserId}.", userId);
                }
            }
            return [];
        }

        var roleId = userState.RoleId;

        // Direct 3-way LINQ Join projection as per ADR-016
        var permissions = await (from rpg in _readDb.RolePermissionGroups
                                 join pgi in _readDb.PermissionGroupItems on rpg.GroupId equals pgi.GroupId
                                 join p in _readDb.Permissions on pgi.PermissionId equals p.Id
                                 where rpg.RoleId == roleId
                                 select $"{p.Resource}:{p.Action}:{p.Scope}".ToLowerInvariant())
                                 .Distinct()
                                 .ToListAsync(cancellationToken);

        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
