namespace SuperMarket.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Contract for the atomic Unit of Work in the Identity application layer.
/// Coordinates the commitment of tracked aggregate changes across all repositories
/// within a single database transaction.
/// </summary>
public interface IUnitOfWork
{
    
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
