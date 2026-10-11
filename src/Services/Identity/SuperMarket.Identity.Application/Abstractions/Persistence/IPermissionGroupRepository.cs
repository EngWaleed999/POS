using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Write-side repository contract for the PermissionGroup Entity.
/// Manages persistence, lifecycle, and groupings of authorization rules.
/// </summary>
public interface IPermissionGroupRepository
{
      Task<PermissionGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

   
        Task<bool> ExistsByNameAsync(string groupName, CancellationToken cancellationToken = default);

    
        void Add(PermissionGroup group);

        void Remove(PermissionGroup group);

        /// <summary>
        /// Validates that all supplied permission group identifiers exist in the database.
        /// </summary>
        Task<bool> AllExistAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken = default);
}
