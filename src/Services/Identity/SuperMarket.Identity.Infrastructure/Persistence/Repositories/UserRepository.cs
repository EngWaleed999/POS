using Microsoft.EntityFrameworkCore;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Infrastructure.Data;

namespace SuperMarket.Identity.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(IdentityDbContext dbContext)
    : Repository<User>(dbContext), IUserRepository
{
    public async Task<User?> GetByKeycloakUserIdAsync(string keycloakUserId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId, cancellationToken);
    }

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AnyAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<bool> ExistsByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AnyAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AnyAsync(u => u.Email == email, cancellationToken);
    }
}
