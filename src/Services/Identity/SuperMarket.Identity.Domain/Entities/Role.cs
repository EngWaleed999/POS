// Role Entity: Represents a dynamic job role (e.g., Cashier, StoreManager) with activation control and audit tracking.

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class Role : Entity<Guid>, IAuditableEntity, IActivatable
{
    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------
    public string RoleName { get; private set; } = default!;
    public string? Description { get; private set; }

    // -------------------------------------------------------------------------
    // IActivatable (Protected via Explicit Interface Implementation)
    // -------------------------------------------------------------------------
    public bool IsActive { get; private set; } = true;

    bool IActivatable.IsActive
    {
        get => IsActive;
        set => IsActive = value;
    }

    // -------------------------------------------------------------------------
    // IAuditableEntity (Protected via Explicit Interface Implementation)
    // -------------------------------------------------------------------------
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    DateTimeOffset IAuditableEntity.CreatedAt
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }

    string? IAuditableEntity.CreatedBy
    {
        get => CreatedBy;
        set => CreatedBy = value;
    }

    DateTimeOffset? IAuditableEntity.UpdatedAt
    {
        get => UpdatedAt;
        set => UpdatedAt = value;
    }

    string? IAuditableEntity.UpdatedBy
    {
        get => UpdatedBy;
        set => UpdatedBy = value;
    }

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private Role()
    {
    }

    private Role(Guid id, string roleName, string? description)
        : base(id)
    {
        RoleName = roleName;
        Description = description;
        IsActive = true;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<Role> Create(string roleName, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return Result.Failure<Role>(RoleErrors.EmptyRoleName);

        var role = new Role(
            id: Guid.NewGuid(),
            roleName: roleName.Trim(),
            description: description?.Trim());

        return Result.Success(role);
    }

    // -------------------------------------------------------------------------
    // Domain Business Behaviors
    // -------------------------------------------------------------------------
    public Result Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return Result.Failure(RoleErrors.EmptyRoleName);

        RoleName = newName.Trim();
        return Result.Success();
    }

    public Result UpdateDescription(string? description)
    {
        Description = description?.Trim();
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(RoleErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure(RoleErrors.AlreadyDeactivated);

        IsActive = false;
        return Result.Success();
    }
}
