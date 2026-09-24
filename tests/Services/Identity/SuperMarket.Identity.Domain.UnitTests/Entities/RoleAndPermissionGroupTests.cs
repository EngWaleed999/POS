using FluentAssertions;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.UnitTests.Entities;

public sealed class RoleAndPermissionGroupTests
{
    // =========================================================================
    // 1. Permission Atomic Entity ([Theory] + [InlineData])
    // =========================================================================

    [Fact]
    public void Permission_Create_ShouldSucceedAndNormalizeFormat_WhenInputsAreValid()
    {
        // Act
        var result = Permission.Create("   SHIFTS   ", "   OPEN   ", "   OWN   ", "   Sales Operations   ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var permission = result.Value;
        permission.Resource.Should().Be("shifts");
        permission.Action.Should().Be("open");
        permission.Scope.Should().Be("own");
        permission.Category.Should().Be("Sales Operations");
    }

    [Theory]
    // Empty Resource
    [InlineData("", "open", "own", "Sales", "Permission.EmptyResource")]
    [InlineData("   ", "open", "own", "Sales", "Permission.EmptyResource")]
    // Empty Action
    [InlineData("shifts", "", "own", "Sales", "Permission.EmptyAction")]
    [InlineData("shifts", "   ", "own", "Sales", "Permission.EmptyAction")]
    // Empty Scope
    [InlineData("shifts", "open", "", "Sales", "Permission.EmptyScope")]
    [InlineData("shifts", "open", "   ", "Sales", "Permission.EmptyScope")]
    // Empty Category
    [InlineData("shifts", "open", "own", "", "Permission.EmptyCategory")]
    [InlineData("shifts", "open", "own", "   ", "Permission.EmptyCategory")]
    public void Permission_Create_ShouldFail_WhenRequiredFieldsAreMissing(
        string r, string a, string s, string c, string expectedError)
    {
        Permission.Create(r, a, s, c).Error.Code.Should().Be(expectedError);
    }

    // =========================================================================
    // 2. PermissionGroup Entity ([Theory] + [InlineData])
    // =========================================================================

    [Fact]
    public void PermissionGroup_Lifecycle_ShouldManageNameAndDescription()
    {
        // Create
        var group = PermissionGroup.Create("Cashier Core Operations", "Core operations").Value;
        group.GroupName.Should().Be("Cashier Core Operations");

        // Rename
        group.Rename("POS Core").IsSuccess.Should().BeTrue();
        group.GroupName.Should().Be("POS Core");

        // Update Description
        group.UpdateDescription("Updated desc").IsSuccess.Should().BeTrue();
        group.Description.Should().Be("Updated desc");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PermissionGroup_CreateAndRename_ShouldFail_WhenNameIsEmpty(string? invalidName)
    {
        PermissionGroup.Create(invalidName!).Error.Should().Be(PermissionGroupErrors.EmptyGroupName);

        var group = PermissionGroup.Create("ValidGroup").Value;
        group.Rename(invalidName!).Error.Should().Be(PermissionGroupErrors.EmptyGroupName);
    }

    // =========================================================================
    // 3. Role Entity & State Machine ([Theory] + [InlineData])
    // =========================================================================

    [Fact]
    public void Role_LifecycleAndActivation_ShouldToggleStateCorrectly()
    {
        var role = Role.Create("StoreManager", "Manager role").Value;
        role.RoleName.Should().Be("StoreManager");
        role.IsActive.Should().BeTrue();

        // Rename & Description
        role.Rename("SeniorStoreManager").IsSuccess.Should().BeTrue();
        role.RoleName.Should().Be("SeniorStoreManager");
        role.UpdateDescription("Senior role").IsSuccess.Should().BeTrue();

        // Deactivate
        role.Deactivate().IsSuccess.Should().BeTrue();
        role.IsActive.Should().BeFalse();
        role.Deactivate().Error.Should().Be(RoleErrors.AlreadyDeactivated);

        // Activate
        role.Activate().IsSuccess.Should().BeTrue();
        role.IsActive.Should().BeTrue();
        role.Activate().Error.Should().Be(RoleErrors.AlreadyActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Role_CreateAndRename_ShouldFail_WhenNameIsEmpty(string? invalidName)
    {
        Role.Create(invalidName!).Error.Should().Be(RoleErrors.EmptyRoleName);

        var role = Role.Create("ValidRole").Value;
        role.Rename(invalidName!).Error.Should().Be(RoleErrors.EmptyRoleName);
    }

    // =========================================================================
    // 4. Pure Join Entities: PermissionGroupItem & RolePermissionGroup
    // =========================================================================

    [Theory]
    [InlineData(true, false, "PermissionGroup.EmptyGroupId")]      // GroupId is empty
    [InlineData(false, true, "Permission.EmptyPermissionId")]     // PermissionId is empty
    public void PermissionGroupItem_Create_ShouldValidateCompositeKey(bool emptyGroup, bool emptyPermission, string expectedError)
    {
        var groupId = emptyGroup ? Guid.Empty : Guid.NewGuid();
        var permissionId = emptyPermission ? Guid.Empty : Guid.NewGuid();

        PermissionGroupItem.Create(groupId, permissionId).Error.Code.Should().Be(expectedError);
    }

    [Theory]
    [InlineData(true, false, "Role.EmptyRoleId")]                 // RoleId is empty
    [InlineData(false, true, "PermissionGroup.EmptyGroupId")]      // GroupId is empty
    public void RolePermissionGroup_Create_ShouldValidateCompositeKey(bool emptyRole, bool emptyGroup, string expectedError)
    {
        var roleId = emptyRole ? Guid.Empty : Guid.NewGuid();
        var groupId = emptyGroup ? Guid.Empty : Guid.NewGuid();

        RolePermissionGroup.Create(roleId, groupId).Error.Code.Should().Be(expectedError);
    }

    // =========================================================================
    // 5. End-to-End Domain RBAC Association Flow
    // =========================================================================

    [Fact]
    public void RBAC_CompleteAssociationGraph_ShouldLinkAcrossAllFiveEntities()
    {
        // 1. Create Atomic Permission (resource:action:scope)
        var permission = Permission.Create("shifts", "open", "own", "POS").Value;

        // 2. Create Permission Group
        var group = PermissionGroup.Create("Cashier Shift Operations").Value;

        // 3. Link Permission to Group via Composite Join Entity
        var groupItemResult = PermissionGroupItem.Create(group.Id, permission.Id);
        groupItemResult.IsSuccess.Should().BeTrue();
        groupItemResult.Value.GroupId.Should().Be(group.Id);
        groupItemResult.Value.PermissionId.Should().Be(permission.Id);

        // 4. Create Role
        var role = Role.Create("Cashier", "Front-desk POS operator").Value;

        // 5. Link Role to Group via Composite Join Entity
        var roleGroupResult = RolePermissionGroup.Create(role.Id, group.Id);
        roleGroupResult.IsSuccess.Should().BeTrue();
        roleGroupResult.Value.RoleId.Should().Be(role.Id);
        roleGroupResult.Value.GroupId.Should().Be(group.Id);
    }
}
