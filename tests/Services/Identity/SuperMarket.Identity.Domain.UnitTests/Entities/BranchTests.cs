using FluentAssertions;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;
using SuperMarket.Identity.Domain.Events;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Domain.UnitTests.Entities;

public sealed class BranchTests
{
    private static BranchCode CreateValidBranchCode() =>
        BranchCode.Create("BR-MAIN-01").Value;

    private static Address CreateValidAddress() =>
        Address.Create("King Fahd Road", "Riyadh", "Central", "12345").Value;

    private static Branch CreateValidBranch()
    {
        return Branch.Create(
            code: CreateValidBranchCode(),
            name: "Main Flagship Branch",
            address: CreateValidAddress(),
            phone: "+966500000001",
            taxNumber: "300000000000003",
            email: "main@supermarket.com",
            currency: "SAR").Value;
    }

    // =========================================================================
    // 1. Creation & Domain Event (الإنشاء وإطلاق الأحداث)
    // =========================================================================

    [Fact]
    public void Create_ShouldSucceedAndEmitBranchCreatedDomainEvent_WhenAllInputsAreValid()
    {
        // Arrange
        var code = CreateValidBranchCode();
        var address = CreateValidAddress();

        // Act
        var result = Branch.Create(
            code: code,
            name: "   Al-Yasmin Branch   ",
            address: address,
            phone: "   +966511111111   ",
            taxNumber: "   300111111111113   ",
            email: "   yasmin@supermarket.com   ",
            currency: "sar");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var branch = result.Value;
        branch.Name.Should().Be("Al-Yasmin Branch");
        branch.Phone.Should().Be("+966511111111");
        branch.TaxNumber.Should().Be("300111111111113");
        branch.Email.Should().Be("yasmin@supermarket.com");
        branch.Currency.Should().Be("SAR");
        branch.IsActive.Should().BeTrue();
        branch.IsDeleted.Should().BeFalse();

        // Verify Domain Event Emission
        branch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<BranchCreatedDomainEvent>()
            .Which.Should().Match<BranchCreatedDomainEvent>(e =>
                e.BranchId == branch.Id &&
                e.BranchCode == "BR-MAIN-01" &&
                e.BranchName == "Al-Yasmin Branch" &&
                e.City == "Riyadh" &&
                e.Phone == "+966511111111" &&
                e.TaxNumber == "300111111111113");
    }

    // =========================================================================
    // 2. Data-Driven Creation Invariants Matrix ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    // Empty Name
    [InlineData(null, "+966500000000", "300000000000001", "Branch.EmptyBranchName")]
    [InlineData("", "+966500000000", "300000000000001", "Branch.EmptyBranchName")]
    [InlineData("   ", "+966500000000", "300000000000001", "Branch.EmptyBranchName")]
    // Empty Phone
    [InlineData("Valid Branch", null, "300000000000001", "Branch.EmptyPhone")]
    [InlineData("Valid Branch", "", "300000000000001", "Branch.EmptyPhone")]
    [InlineData("Valid Branch", "   ", "300000000000001", "Branch.EmptyPhone")]
    // Empty Tax Number
    [InlineData("Valid Branch", "+966500000000", null, "Branch.EmptyTaxNumber")]
    [InlineData("Valid Branch", "+966500000000", "", "Branch.EmptyTaxNumber")]
    [InlineData("Valid Branch", "+966500000000", "   ", "Branch.EmptyTaxNumber")]
    public void Create_ShouldFail_WhenRequiredStringFieldsAreMissing(
        string? name,
        string? phone,
        string? taxNumber,
        string expectedErrorCode)
    {
        // Act
        var result = Branch.Create(
            code: CreateValidBranchCode(),
            name: name!,
            address: CreateValidAddress(),
            phone: phone!,
            taxNumber: taxNumber!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    [Theory]
    [InlineData(true, "Branch.EmptyBranchCode")] // code is null
    [InlineData(false, "Branch.EmptyStreet")]    // address is null
    public void Create_ShouldFail_WhenValueObjectsAreNull(bool isCodeNull, string expectedErrorCode)
    {
        // Act
        var result = Branch.Create(
            code: isCodeNull ? null! : CreateValidBranchCode(),
            name: "Branch Name",
            address: isCodeNull ? CreateValidAddress() : null!,
            phone: "+966500000000",
            taxNumber: "300000000000001");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    // =========================================================================
    // 3. Updates & Operating Hours Management
    // =========================================================================

    [Fact]
    public void UpdateDetailsAndAddress_ShouldSucceed_WhenValidInputsProvided()
    {
        // Arrange
        var branch = CreateValidBranch();
        var newAddress = Address.Create("Olaya Street", "Riyadh", "Central", "11564").Value;

        // Act
        var detailsResult = branch.UpdateDetails("Updated Name", "+966599999999", "300999999999999", "new@supermarket.com");
        var addressResult = branch.UpdateAddress(newAddress);

        // Assert
        detailsResult.IsSuccess.Should().BeTrue();
        addressResult.IsSuccess.Should().BeTrue();
        branch.Name.Should().Be("Updated Name");
        branch.Address.Should().Be(newAddress);
    }

    [Fact]
    public void SetOperatingHours_ShouldManageScheduleAndEnforceUniqueDays()
    {
        // Arrange
        var branch = CreateValidBranch();
        var sat1 = BranchOperatingHours.Create(branch.Id, DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(14, 0)).Value;
        var sat2 = BranchOperatingHours.Create(branch.Id, DayOfWeek.Saturday, new TimeOnly(16, 0), new TimeOnly(23, 0)).Value;
        var sun = BranchOperatingHours.Create(branch.Id, DayOfWeek.Sunday, new TimeOnly(8, 0), new TimeOnly(23, 0)).Value;

        // Act & Assert 1: Reject duplicate day in batch
        var duplicateResult = branch.SetOperatingHours([sat1, sat2]);
        duplicateResult.IsFailure.Should().BeTrue();
        duplicateResult.Error.Should().Be(BranchErrors.DuplicateDayOfWeek);

        // Act & Assert 2: Accept distinct days
        var validResult = branch.SetOperatingHours([sat1, sun]);
        validResult.IsSuccess.Should().BeTrue();
        branch.OperatingHours.Should().HaveCount(2);

        // Act & Assert 3: AddOrUpdate modifies single day
        branch.AddOrUpdateOperatingHour(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(22, 0));
        branch.OperatingHours.Single(h => h.DayOfWeek == DayOfWeek.Sunday).OpenTime.Should().Be(new TimeOnly(10, 0));
    }

    // =========================================================================
    // 4. State Machine Transitions ([Theory] + [InlineData])
    // =========================================================================

    [Fact]
    public void ActivationAndDeactivation_ShouldToggleStateCorrectly()
    {
        // Arrange
        var branch = CreateValidBranch();

        // 1. Deactivate active branch -> Succeeds
        branch.Deactivate().IsSuccess.Should().BeTrue();
        branch.IsActive.Should().BeFalse();

        // 2. Double Deactivate -> Fails with AlreadyDeactivated
        branch.Deactivate().Error.Should().Be(BranchErrors.AlreadyDeactivated);

        // 3. Activate inactive branch -> Succeeds
        branch.Activate().IsSuccess.Should().BeTrue();
        branch.IsActive.Should().BeTrue();

        // 4. Double Activate -> Fails with AlreadyActive
        branch.Activate().Error.Should().Be(BranchErrors.AlreadyActive);
    }

    // =========================================================================
    // 5. Soft Delete & Immutability Invariant (تجميد الكيان المحذوف)
    // =========================================================================

    [Fact]
    public void SoftDelete_ShouldDeactivateAndBlockAllFutureMutations()
    {
        // Arrange
        var branch = CreateValidBranch();
        var deletedBy = "SuperAdminUser";

        // Act: Delete
        var deleteResult = branch.SoftDelete(deletedBy);

        // Assert deletion metadata
        deleteResult.IsSuccess.Should().BeTrue();
        branch.IsDeleted.Should().BeTrue();
        branch.IsActive.Should().BeFalse();
        branch.DeletedBy.Should().Be(deletedBy);

        // Double delete fails
        branch.SoftDelete("Admin2").Error.Should().Be(BranchErrors.AlreadyDeleted);

        // Any mutation on deleted branch fails with DeletedBranchCannotBeModified
        branch.UpdateDetails("Name", "0500000000", "300000000000001").Error.Should().Be(BranchErrors.DeletedBranchCannotBeModified);
        branch.UpdateAddress(CreateValidAddress()).Error.Should().Be(BranchErrors.DeletedBranchCannotBeModified);
        branch.Activate().Error.Should().Be(BranchErrors.DeletedBranchCannotBeModified);
        branch.Deactivate().Error.Should().Be(BranchErrors.DeletedBranchCannotBeModified);
        branch.SetOperatingHours([]).Error.Should().Be(BranchErrors.DeletedBranchCannotBeModified);
    }
}
