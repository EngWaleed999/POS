using FluentAssertions;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.Events;

namespace SuperMarket.Identity.Domain.UnitTests.Entities;

public sealed class UserTests
{
    private static readonly Guid ValidRoleId = Guid.NewGuid();
    private static readonly Guid AnotherRoleId = Guid.NewGuid();
    private static readonly Guid ValidBranchId = Guid.NewGuid();
    private static readonly Guid AnotherBranchId = Guid.NewGuid();

    private static User CreateUser() =>
        User.Create("cashier01", "+966501111111", "kc-1", "Ahmed", ValidRoleId, "ahmed@store.com", ValidBranchId).Value;

    // =========================================================================
    // 1. Factory & Domain Event
    // =========================================================================

    [Fact]
    public void Create_ShouldSucceedAndEmitUserCreatedDomainEvent_WhenValid()
    {
        var result = User.Create("  cashier01  ", "  +966501111111  ", "  kc-1  ", "  Ahmed  ", ValidRoleId, "  Ahmed@STORE.COM  ", ValidBranchId);

        result.IsSuccess.Should().BeTrue();
        var user = result.Value;
        user.Username.Should().Be("cashier01");
        user.Email.Should().Be("ahmed@store.com");
        user.IsActive.Should().BeTrue();

        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserCreatedDomainEvent>()
            .Which.Should().Match<UserCreatedDomainEvent>(e =>
                e.UserId == user.Id && e.Username == "cashier01" && e.Email == "ahmed@store.com");
    }

    // =========================================================================
    // 2. Data-Driven Validation Matrix ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    [InlineData("", "+966500000000", "kc-1", "Name", "User.EmptyUsername")]
    [InlineData("   ", "+966500000000", "kc-1", "Name", "User.EmptyUsername")]
    [InlineData("user", "", "kc-1", "Name", "User.EmptyPhoneNumber")]
    [InlineData("user", "   ", "kc-1", "Name", "User.EmptyPhoneNumber")]
    [InlineData("user", "+966500000000", "", "Name", "User.EmptyKeycloakUserId")]
    [InlineData("user", "+966500000000", "   ", "Name", "User.EmptyKeycloakUserId")]
    [InlineData("user", "+966500000000", "kc-1", "", "User.EmptyFullName")]
    [InlineData("user", "+966500000000", "kc-1", "   ", "User.EmptyFullName")]
    public void Create_ShouldFail_WhenRequiredStringFieldsAreMissing(string u, string p, string k, string n, string err)
    {
        User.Create(u, p, k, n, ValidRoleId).Error.Code.Should().Be(err);
    }

    [Fact]
    public void Create_ShouldFail_WithEmptyRoleId_WhenRoleIdIsGuidEmpty() =>
        User.Create("user", "+966500000000", "kc-1", "Name", Guid.Empty).Error.Should().Be(UserErrors.EmptyRoleId);

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("USER@STORE.COM", "user@store.com")]
    public void Create_ShouldNormalizeEmail_ToLowerCaseOrNull(string? input, string? expected) =>
        User.Create("u", "+966500000000", "kc-1", "n", ValidRoleId, input).Value.Email.Should().Be(expected);

    // =========================================================================
    // 3. POS PIN Management
    // =========================================================================

    [Theory]
    [InlineData("valid_hash", true, null)]
    [InlineData("", false, "User.EmptyPinHash")]
    [InlineData("   ", false, "User.EmptyPinHash")]
    public void SetPin_ShouldValidateAndAssignHash(string pin, bool success, string? err)
    {
        var user = CreateUser();
        var result = user.SetPin(pin);
        result.IsSuccess.Should().Be(success);
        if (success) user.PinHash.Should().Be(pin);
        else result.Error.Code.Should().Be(err);
    }

    [Fact]
    public void RemovePin_ShouldClearPinHash()
    {
        var user = CreateUser();
        user.SetPin("hash");
        user.RemovePin().IsSuccess.Should().BeTrue();
        user.PinHash.Should().BeNull();
    }

    // =========================================================================
    // 4. Brute-Force & Lockout Security Workflow
    // =========================================================================

    [Fact]
    public void LockoutLifecycle_ShouldLockAtThreeAttempts_AndResetUponPostExpiryLogin()
    {
        var user = CreateUser();
        var now = DateTimeOffset.UtcNow;

        // 1 & 2: Failures under threshold
        user.RecordFailedLogin(now);
        user.RecordFailedLogin(now);
        user.IsLockedOut(now).Should().BeFalse();

        // 3: Threshold reached -> Lockout triggered + Event emitted
        user.RecordFailedLogin(now);
        user.IsLockedOut(now).Should().BeTrue();
        user.LockoutEnd.Should().Be(now.AddMinutes(15));
        user.DomainEvents.Should().ContainSingle(e => e is UserLockedOutDomainEvent);

        // Block attempts during lockout
        user.RecordFailedLogin(now).Error.Should().Be(UserErrors.AccountLockedOut);
        user.RecordLogin(now).Error.Should().Be(UserErrors.AccountLockedOut);

        // Success post-lockout resets counters
        var postLockout = now.AddMinutes(16);
        user.RecordLogin(postLockout).IsSuccess.Should().BeTrue();
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
        user.LastLogin.Should().Be(postLockout);

        // Manual Unlock
        user.RecordFailedLogin(now);
        user.Unlock().IsSuccess.Should().BeTrue();
        user.AccessFailedCount.Should().Be(0);
    }

    // =========================================================================
    // 5. Branch Transfers & Role Transitions
    // =========================================================================

    [Fact]
    public void BranchTransferAndRoleChange_ShouldEmitEvents_AndIgnoreIdempotentCalls()
    {
        var user = CreateUser();

        // Transfer to another branch -> Event emitted
        user.AssignToBranch(AnotherBranchId).IsSuccess.Should().BeTrue();
        user.BranchId.Should().Be(AnotherBranchId);
        user.DomainEvents.Should().ContainSingle(e => e is UserTransferredDomainEvent);

        // Idempotent reassignment to same branch -> No duplicate event
        user.ClearDomainEvents();
        user.AssignToBranch(AnotherBranchId).IsSuccess.Should().BeTrue();
        user.DomainEvents.Should().BeEmpty();

        // Role change -> Event emitted
        user.ChangeRole(AnotherRoleId).IsSuccess.Should().BeTrue();
        user.RoleId.Should().Be(AnotherRoleId);
        user.DomainEvents.Should().ContainSingle(e => e is UserRoleChangedDomainEvent);

        // Empty role fails
        user.ChangeRole(Guid.Empty).Error.Should().Be(UserErrors.EmptyRoleId);
    }

    // =========================================================================
    // 6. Activation, Deactivation & Soft Delete Immutability
    // =========================================================================

    [Fact]
    public void ActivationToggle_ShouldChangeStateCorrectly()
    {
        var user = CreateUser();
        user.Deactivate().IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.DomainEvents.Should().ContainSingle(e => e is UserDeactivatedDomainEvent);
        user.Deactivate().Error.Should().Be(UserErrors.AlreadyDeactivated);

        user.Activate().IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        user.Activate().Error.Should().Be(UserErrors.AlreadyActive);
    }

    [Fact]
    public void SoftDelete_ShouldDeactivateAndBlockAllMutations()
    {
        var user = CreateUser();
        user.SoftDelete("AdminUser", "Resigned").IsSuccess.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.SoftDelete("AdminUser").Error.Should().Be(UserErrors.AlreadyDeleted);

        // All 11 mutation methods must fail with DeletedUserCannotBeModified
        var mutations = new Func<BuildingBlocks.Results.Result>[]
        {
            () => user.SetPin("h"), () => user.RemovePin(), () => user.UpdateProfile("n"),
            () => user.UpdateContactInfo("+966509999999", null), () => user.ChangeRole(AnotherRoleId),
            () => user.AssignToBranch(AnotherBranchId), () => user.Activate(), () => user.Deactivate(),
            () => user.RecordFailedLogin(DateTimeOffset.UtcNow), () => user.RecordLogin(DateTimeOffset.UtcNow),
            () => user.Unlock()
        };

        foreach (var mutation in mutations)
        {
            mutation().Error.Should().Be(UserErrors.DeletedUserCannotBeModified);
        }
    }
}
