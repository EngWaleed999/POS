using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Write-side repository contract for the POSRegister Aggregate Root.
/// Enforces hardware terminal boundary and store affinity.
/// </summary>
public interface IPOSRegisterRepository
{
   
    Task<POSRegister?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

   
    Task<bool> ExistsByCodeInBranchAsync(Guid branchId, RegisterCode code, CancellationToken cancellationToken = default);

  
    void Add(POSRegister register);

   
    void Remove(POSRegister register);
}
