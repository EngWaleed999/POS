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
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);
    private const string KeyPrefix = "pos:perm:usr:";

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
        var db = GetDatabase();

        // 1. Check Redis Cache (Hit Path)
        if (db is not null)
        {
            try
            {
                if (await db.KeyExistsAsync(cacheKey))
                {
                    return await db.SetContainsAsync(cacheKey, normalizedPermission);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis error while reading cache key {Key}. Falling back to database.", cacheKey);
            }
        }

        // 2. Cache Miss: Execute optimized LINQ Projection from PostgreSQL
        var permissions = await FetchPermissionsFromDatabaseAsync(userId, cancellationToken);

        // 3. Populate Redis Cache
        if (db is not null && permissions.Count > 0)
        {
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
        var db = GetDatabase();

        // 1. Check Redis Cache
        if (db is not null)
        {
            try
            {
                if (await db.KeyExistsAsync(cacheKey))
                {
                    var members = await db.SetMembersAsync(cacheKey);
                    return members.Select(m => m.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis error while reading cache key {Key}. Falling back to database.", cacheKey);
            }
        }

        // 2. Cache Miss: Query Database
        var permissions = await FetchPermissionsFromDatabaseAsync(userId, cancellationToken);

        // 3. Populate Redis Cache
        if (db is not null && permissions.Count > 0)
        {
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
        try
        {
            await db.KeyDeleteAsync(cacheKey);
            _logger.LogInformation("Invalidated cached permissions for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate Redis cache key {Key}", cacheKey);
        }
    }

    public async Task InvalidateRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var db = GetDatabase();
        if (db is null) return;

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
            _logger.LogWarning(ex, "Failed to invalidate role permissions for role {RoleId}", roleId);
        }
    }

    private static string GetCacheKey(Guid userId) => $"{KeyPrefix}{userId:N}";

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
        var roleId = await _readDb.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive && !u.IsDeleted)
            .Select(u => (Guid?)u.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId is null)
        {
            return [];
        }

        // Direct 3-way LINQ Join projection as per ADR-016
        var permissions = await (from rpg in _readDb.RolePermissionGroups
                                 join pgi in _readDb.PermissionGroupItems on rpg.GroupId equals pgi.GroupId
                                 join p in _readDb.Permissions on pgi.PermissionId equals p.Id
                                 where rpg.RoleId == roleId.Value
                                 select $"{p.Resource}:{p.Action}:{p.Scope}".ToLowerInvariant())
                                 .Distinct()
                                 .ToListAsync(cancellationToken);

        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
