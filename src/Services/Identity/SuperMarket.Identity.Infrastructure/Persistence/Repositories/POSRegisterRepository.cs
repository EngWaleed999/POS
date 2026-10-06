using Microsoft.EntityFrameworkCore;
using SuperMarket.Identity.Application.Abstractions.Persistence;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.ValueObjects;
using SuperMarket.Identity.Infrastructure.Data;

namespace SuperMarket.Identity.Infrastructure.Persistence.Repositories;

internal sealed class POSRegisterRepository(IdentityDbContext dbContext)
    : Repository<POSRegister>(dbContext), IPOSRegisterRepository
{
    public async Task<bool> ExistsByCodeInBranchAsync(Guid branchId, RegisterCode code, CancellationToken cancellationToken = default)
    {
        return await DbContext.POSRegisters
            .AnyAsync(r => r.BranchId == branchId && r.RegisterCode == code, cancellationToken);
    }
}
