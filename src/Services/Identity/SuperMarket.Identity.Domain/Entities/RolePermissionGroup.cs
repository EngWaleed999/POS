// RolePermissionGroup: Join entity linking a Role to a PermissionGroup via composite key (RoleId, GroupId).

using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.Entities;

public sealed class RolePermissionGroup
{
    // -------------------------------------------------------------------------
    // Composite Key Properties
    // -------------------------------------------------------------------------
    public Guid RoleId { get; private set; }
    public Guid GroupId { get; private set; }

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private RolePermissionGroup()
    {
    }

    private RolePermissionGroup(Guid roleId, Guid groupId)
    {
        RoleId = roleId;
        GroupId = groupId;
    }

    // -------------------------------------------------------------------------
    // Factory Method
    // -------------------------------------------------------------------------
    public static Result<RolePermissionGroup> Create(Guid roleId, Guid groupId)
    {
        if (roleId == Guid.Empty)
            return Result.Failure<RolePermissionGroup>(RoleErrors.EmptyRoleId);

        if (groupId == Guid.Empty)
            return Result.Failure<RolePermissionGroup>(PermissionGroupErrors.EmptyGroupId);

        var link = new RolePermissionGroup(roleId, groupId);
        return Result.Success(link);
    }
}
