// PermissionGroup Entity: Groups related permissions into reusable packages (e.g., "Cashier Core Package").

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class PermissionGroup : Entity<Guid>
{
    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------
    public string GroupName { get; private set; } = default!;
    public string? Description { get; private set; }

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private PermissionGroup()
    {
    }

    private PermissionGroup(Guid id, string groupName, string? description)
        : base(id)
    {
        GroupName = groupName;
        Description = description;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<PermissionGroup> Create(string groupName, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return Result.Failure<PermissionGroup>(PermissionGroupErrors.EmptyGroupName);

        var group = new PermissionGroup(
            id: Guid.NewGuid(),
            groupName: groupName.Trim(),
            description: description?.Trim());

        return Result.Success(group);
    }

    // -------------------------------------------------------------------------
    // Domain Business Behaviors
    // -------------------------------------------------------------------------
    public Result Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return Result.Failure(PermissionGroupErrors.EmptyGroupName);

        GroupName = newName.Trim();
        return Result.Success();
    }

    public Result UpdateDescription(string? description)
    {
        Description = description?.Trim();
        return Result.Success();
    }
}
