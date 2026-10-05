using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Application.Abstractions.Persistence;


public interface IBranchRepository
{
   
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

  
    Task<bool> ExistsByCodeAsync(BranchCode code, CancellationToken cancellationToken = default);

        void Add(Branch branch); 
        void Remove(Branch branch);
}
