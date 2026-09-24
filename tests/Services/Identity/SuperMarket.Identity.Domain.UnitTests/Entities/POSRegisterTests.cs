using FluentAssertions;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.Events;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Domain.UnitTests.Entities;

public sealed class POSRegisterTests
{
    private static readonly Guid ValidBranchId = Guid.NewGuid();
    private static readonly Guid AnotherBranchId = Guid.NewGuid();

    private static RegisterCode CreateValidRegisterCode(string code = "POS-01") =>
        RegisterCode.Create(code).Value;

    private static POSRegister CreateValidRegister()
    {
        return POSRegister.Create(
            branchId: ValidBranchId,
            registerCode: CreateValidRegisterCode(),
            name: "Checkout Register 01",
            terminalIpOrFingerprint: "192.168.1.101").Value;
    }

    // =========================================================================
    // 1. Factory & Domain Event (الإنشاء وإطلاق الأحداث)
    // =========================================================================

    [Fact]
    public void Create_ShouldSucceedAndEmitPOSRegisterCreatedDomainEvent_WhenAllInputsAreValid()
    {
        // Arrange
        var branchId = ValidBranchId;
        var code = CreateValidRegisterCode("POS-REG-01");

        // Act
        var result = POSRegister.Create(
            branchId: branchId,
            registerCode: code,
            name: "   Express Checkout 01   ",
            terminalIpOrFingerprint: "   MAC-AA-BB-CC-DD-EE-FF   ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var register = result.Value;
        register.BranchId.Should().Be(branchId);
        register.RegisterCode.Should().Be(code);
        register.Name.Should().Be("Express Checkout 01");
        register.TerminalIpOrFingerprint.Should().Be("MAC-AA-BB-CC-DD-EE-FF");
        register.IsActive.Should().BeTrue();
        register.IsDeleted.Should().BeFalse();

        // Verify Domain Event Emission
        register.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<POSRegisterCreatedDomainEvent>()
            .Which.Should().Match<POSRegisterCreatedDomainEvent>(e =>
                e.RegisterId == register.Id &&
                e.BranchId == branchId &&
                e.RegisterCode == "POS-REG-01" &&
                e.Name == "Express Checkout 01" &&
                e.TerminalIpOrFingerprint == "MAC-AA-BB-CC-DD-EE-FF");
    }

    // =========================================================================
    // 2. Data-Driven Validation Matrix ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    // Empty Name
    [InlineData(null, "POSRegister.EmptyRegisterName")]
    [InlineData("", "POSRegister.EmptyRegisterName")]
    [InlineData("   ", "POSRegister.EmptyRegisterName")]
    public void Create_ShouldFail_WhenNameIsNullOrWhiteSpace(string? invalidName, string expectedErrorCode)
    {
        // Act
        var result = POSRegister.Create(
            branchId: ValidBranchId,
            registerCode: CreateValidRegisterCode(),
            name: invalidName!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    [Theory]
    [InlineData(true, "POSRegister.EmptyBranchId")]      // Empty branch ID
    [InlineData(false, "POSRegister.EmptyRegisterCode")]  // Null register code
    public void Create_ShouldFail_WhenRequiredIdentifiersAreMissing(bool isBranchEmpty, string expectedErrorCode)
    {
        // Act
        var result = POSRegister.Create(
            branchId: isBranchEmpty ? Guid.Empty : ValidBranchId,
            registerCode: isBranchEmpty ? CreateValidRegisterCode() : null!,
            name: "Register Name");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("192.168.1.50", "192.168.1.50")]
    public void FingerprintHandling_ShouldNormalizeWhitespaceToNullOrRetainValue(string? inputFingerprint, string? expectedFingerprint)
    {
        // Act
        var result = POSRegister.Create(
            branchId: ValidBranchId,
            registerCode: CreateValidRegisterCode(),
            name: "Checkout 02",
            terminalIpOrFingerprint: inputFingerprint);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TerminalIpOrFingerprint.Should().Be(expectedFingerprint);
    }

    // =========================================================================
    // 3. Updates & Branch Reassignment (التعديل وإعادة التوجيه لفرع آخر)
    // =========================================================================

    [Fact]
    public void UpdateDetailsAndFingerprint_ShouldModifyStateCorrectly()
    {
        // Arrange
        var register = CreateValidRegister();

        // Act & Assert 1: Valid Update
        register.UpdateDetails("New Kiosk", "10.0.0.1").IsSuccess.Should().BeTrue();
        register.Name.Should().Be("New Kiosk");
        register.TerminalIpOrFingerprint.Should().Be("10.0.0.1");

        // Act & Assert 2: Invalid Name Update
        register.UpdateDetails("   ", "10.0.0.1").Error.Should().Be(POSRegisterErrors.EmptyRegisterName);

        // Act & Assert 3: Clear Fingerprint
        register.UpdateFingerprint("   ").IsSuccess.Should().BeTrue();
        register.TerminalIpOrFingerprint.Should().BeNull();
    }

    [Fact]
    public void ReassignBranch_ShouldValidateBranchIdAndPreventSameBranchReassignment()
    {
        // Arrange
        var register = CreateValidRegister();

        // 1. Success on valid new branch
        register.ReassignBranch(AnotherBranchId).IsSuccess.Should().BeTrue();
        register.BranchId.Should().Be(AnotherBranchId);

        // 2. Failure on Guid.Empty
        register.ReassignBranch(Guid.Empty).Error.Should().Be(POSRegisterErrors.EmptyBranchId);

        // 3. Failure on same branch
        register.ReassignBranch(AnotherBranchId).Error.Should().Be(POSRegisterErrors.SameBranchReassignment);
    }

    // =========================================================================
    // 4. State Machine & Soft Delete
    // =========================================================================

    [Fact]
    public void ActivationAndDeactivation_ShouldToggleStateCorrectly()
    {
        // Arrange
        var register = CreateValidRegister();

        // Deactivate active register
        register.Deactivate().IsSuccess.Should().BeTrue();
        register.IsActive.Should().BeFalse();
        register.Deactivate().Error.Should().Be(POSRegisterErrors.AlreadyDeactivated);

        // Activate inactive register
        register.Activate().IsSuccess.Should().BeTrue();
        register.IsActive.Should().BeTrue();
        register.Activate().Error.Should().Be(POSRegisterErrors.AlreadyActive);
    }

    [Fact]
    public void SoftDelete_ShouldDeactivateAndBlockAllFutureMutations()
    {
        // Arrange
        var register = CreateValidRegister();

        // Soft delete
        register.SoftDelete("admin").IsSuccess.Should().BeTrue();
        register.IsDeleted.Should().BeTrue();
        register.IsActive.Should().BeFalse();

        // Double delete fails
        register.SoftDelete("admin2").Error.Should().Be(POSRegisterErrors.AlreadyDeleted);

        // All mutations fail with DeletedRegisterCannotBeModified
        register.UpdateDetails("Name", null).Error.Should().Be(POSRegisterErrors.DeletedRegisterCannotBeModified);
        register.UpdateFingerprint("1.1.1.1").Error.Should().Be(POSRegisterErrors.DeletedRegisterCannotBeModified);
        register.ReassignBranch(AnotherBranchId).Error.Should().Be(POSRegisterErrors.DeletedRegisterCannotBeModified);
        register.Activate().Error.Should().Be(POSRegisterErrors.DeletedRegisterCannotBeModified);
        register.Deactivate().Error.Should().Be(POSRegisterErrors.DeletedRegisterCannotBeModified);
    }
}
