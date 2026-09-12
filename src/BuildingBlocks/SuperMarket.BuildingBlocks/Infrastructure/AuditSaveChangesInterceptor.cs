using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.Infrastructure;

/// <summary>
/// Entity Framework Core SaveChanges interceptor that automatically enforces:
/// 1. Audit timestamps (<see cref="IAuditableEntity.CreatedAt"/>, <see cref="IAuditableEntity.UpdatedAt"/>) in UTC.
/// 2. User attribution (<see cref="IAuditableEntity.CreatedBy"/>, <see cref="IAuditableEntity.UpdatedBy"/>) from current session.
/// 3. Soft-delete automation (<see cref="ISoftDeletable.IsDeleted"/>, <see cref="ISoftDeletable.DeletedAt"/>, <see cref="ISoftDeletable.DeletedBy"/>)
///    converting hard DELETE commands into soft UPDATE operations.
/// 4. Immutability protection preventing modification of CreatedAt and CreatedBy on existing records.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserContext? _currentUserContext;
    private readonly TimeProvider _timeProvider;

    public AuditSaveChangesInterceptor(
        ICurrentUserContext? currentUserContext = null,
        TimeProvider? timeProvider = null)
    {
        _currentUserContext = currentUserContext;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null)
            return;

        var utcNow = _timeProvider.GetUtcNow();
        var currentUserId = _currentUserContext?.UserId;
        var actor = string.IsNullOrWhiteSpace(currentUserId) ? "SYSTEM" : currentUserId;

        // 1. Process Soft-Delete FIRST
        // This converts EntityState.Deleted to EntityState.Modified, allowing auditable logic to run on it as well.
        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = utcNow;
                entry.Entity.DeletedBy = actor;
            }
        }

        // 2. Process Auditing for Added and Modified entities
        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.CreatedBy = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Enforce immutability: Never allow SQL UPDATE statements to tamper with creation metadata
                entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;

                entry.Entity.UpdatedAt = utcNow;
                entry.Entity.UpdatedBy = actor;
            }
        }
    }
}
