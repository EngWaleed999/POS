// Permission Entity: Represents an atomic authorization rule in the format resource:action:scope.

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class Permission : Entity<Guid>
{
    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------
    public string Resource { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string Scope { get; private set; } = default!;
    public string Category { get; private set; } = default!;

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private Permission()
    {
    }

    private Permission(Guid id, string resource, string action, string scope, string category)
        : base(id)
    {
        Resource = resource;
        Action = action;
        Scope = scope;
        Category = category;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<Permission> Create(string resource, string action, string scope, string category)
    {
        if (string.IsNullOrWhiteSpace(resource))
            return Result.Failure<Permission>(PermissionErrors.EmptyResource);

        if (string.IsNullOrWhiteSpace(action))
            return Result.Failure<Permission>(PermissionErrors.EmptyAction);

        if (string.IsNullOrWhiteSpace(scope))
            return Result.Failure<Permission>(PermissionErrors.EmptyScope);

        if (string.IsNullOrWhiteSpace(category))
            return Result.Failure<Permission>(PermissionErrors.EmptyCategory);

        var permission = new Permission(
            id: Guid.NewGuid(),
            resource: resource.Trim().ToLowerInvariant(),
            action: action.Trim().ToLowerInvariant(),
            scope: scope.Trim().ToLowerInvariant(),
            category: category.Trim());

        return Result.Success(permission);
    }
}
