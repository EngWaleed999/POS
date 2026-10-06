using SuperMarket.Identity.Domain.Entities;

namespace SuperMarket.Identity.Application.Abstractions.Persistence;

/// <summary>
/// Read-side database context contract for the Identity service (CQRS Queries).
/// Exposes read-only, non-tracking queryable streams for fast, projection-based data retrieval.
/// Mutating methods (Add, Update, Remove, SaveChanges) are strictly prohibited here.
/// </summary>
public interface IIdentityReadDbContext
{
   
    IQueryable<Branch> Branches { get; }

   
    IQueryable<User> Users { get; }

    IQueryable<Role> Roles { get; }

    IQueryable<Permission> Permissions { get; }

    IQueryable<PermissionGroup> PermissionGroups { get; }

   
    IQueryable<POSRegister> POSRegisters { get; }
}
