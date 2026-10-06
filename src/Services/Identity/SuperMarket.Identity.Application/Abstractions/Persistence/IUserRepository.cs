using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Write-side repository contract for the User Aggregate Root.
/// Enforces aggregate boundaries and manages persistence for staff and cashier accounts.
/// </summary>
public interface IUserRepository
{
   
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  
    Task<User?> GetByKeycloakUserIdAsync(string keycloakUserId, CancellationToken cancellationToken = default);

 
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);

   
    Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);

   
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

  
    void Add(User user);

  
    void Remove(User user);
}
