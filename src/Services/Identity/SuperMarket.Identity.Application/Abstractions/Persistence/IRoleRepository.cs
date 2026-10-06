using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Write-side repository contract for the Role Entity.
/// Manages persistence, lifecycle, and uniqueness invariants for job roles.
/// </summary>
public interface IRoleRepository
{
   
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  
    Task<bool> ExistsByNameAsync(string roleName, CancellationToken cancellationToken = default);

    void Add(Role role);
    void Remove(Role role);
}
