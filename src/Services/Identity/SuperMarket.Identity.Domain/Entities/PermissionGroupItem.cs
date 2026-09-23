// PermissionGroupItem: Join entity linking a Permission to a PermissionGroup via composite key (GroupId, PermissionId).

using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class PermissionGroupItem
{
    // -------------------------------------------------------------------------
    // Composite Key Properties
    // -------------------------------------------------------------------------
    public Guid GroupId { get; private set; }
    public Guid PermissionId { get; private set; }

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private PermissionGroupItem()
    {
    }

    private PermissionGroupItem(Guid groupId, Guid permissionId)
    {
        GroupId = groupId;
        PermissionId = permissionId;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<PermissionGroupItem> Create(Guid groupId, Guid permissionId)
    {
        if (groupId == Guid.Empty)
            return Result.Failure<PermissionGroupItem>(PermissionGroupErrors.EmptyGroupId);

        if (permissionId == Guid.Empty)
            return Result.Failure<PermissionGroupItem>(PermissionErrors.EmptyPermissionId);

        var item = new PermissionGroupItem(groupId, permissionId);
        return Result.Success(item);
    }
}
