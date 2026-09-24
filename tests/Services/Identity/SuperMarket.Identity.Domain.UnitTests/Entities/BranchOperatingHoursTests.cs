using System.Globalization;
using FluentAssertions;
using SuperMarket.Identity.Domain.Entities;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.UnitTests.Entities;

public sealed class BranchOperatingHoursTests
{
    private static readonly Guid ValidBranchId = Guid.NewGuid();

    private static TimeOnly ParseTime(string time12Hour) =>
        TimeOnly.Parse(time12Hour, CultureInfo.InvariantCulture);

    // =========================================================================
    // 1. Shift Calculations & Boundary Matrix ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    // Happy Path: Daytime Shift (08:00 AM -> 11:00 PM) => IsOvernight = false
    [InlineData("08:00 AM", "11:00 PM", false, false, "08:00 AM", "11:00 PM")]
    // Happy Path: Overnight Shift crossing midnight (04:00 PM -> 02:00 AM) => IsOvernight = true
    [InlineData("04:00 PM", "02:00 AM", false, true, "04:00 PM", "02:00 AM")]
    // Edge Case: Boundary at 1 minute before/after midnight (11:59 PM -> 12:01 AM) => IsOvernight = true
    [InlineData("11:59 PM", "12:01 AM", false, true, "11:59 PM", "12:01 AM")]
    // Happy Path: Day Off / Closed => Times automatically reset to MinValue (12:00 AM) and IsOvernight = false
    [InlineData("09:00 AM", "10:00 PM", true, false, "12:00 AM", "12:00 AM")]
    // Edge Case: Day Off with identical input times => Allowed and reset
    [InlineData("08:00 AM", "08:00 AM", true, false, "12:00 AM", "12:00 AM")]
    public void Create_ShouldCalculateOperatingHoursCorrectly_AcrossAllShiftTypesAndBoundaries(
        string openInput,
        string closeInput,
        bool isClosed,
        bool expectedOvernight,
        string expectedOpen,
        string expectedClose)
    {
        // Arrange
        var openTime = ParseTime(openInput);
        var closeTime = ParseTime(closeInput);

        // Act
        var result = BranchOperatingHours.Create(
            branchId: ValidBranchId,
            dayOfWeek: DayOfWeek.Saturday,
            openTime: openTime,
            closeTime: closeTime,
            isClosed: isClosed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsClosed.Should().Be(isClosed);
        result.Value.IsOvernight.Should().Be(expectedOvernight);
        result.Value.OpenTime.Should().Be(ParseTime(expectedOpen));
        result.Value.CloseTime.Should().Be(ParseTime(expectedClose));
    }

    // =========================================================================
    // 2. Business Invariant Violations ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    // Identical open and close times when the branch is NOT closed (Zero-duration operating schedule)
    [InlineData("09:00 AM", "09:00 AM", false, "BranchOperatingHours.SameOpenAndCloseTime")]
    public void Create_ShouldFail_WhenTimeInvariantIsViolated(
        string openInput,
        string closeInput,
        bool isClosed,
        string expectedErrorCode)
    {
        // Act
        var result = BranchOperatingHours.Create(
            branchId: ValidBranchId,
            dayOfWeek: DayOfWeek.Sunday,
            openTime: ParseTime(openInput),
            closeTime: ParseTime(closeInput),
            isClosed: isClosed);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    [Fact]
    public void Create_ShouldFail_WithEmptyBranchId_WhenBranchIdIsGuidEmpty()
    {
        // Act
        var result = BranchOperatingHours.Create(
            branchId: Guid.Empty,
            dayOfWeek: DayOfWeek.Tuesday,
            openTime: ParseTime("08:00 AM"),
            closeTime: ParseTime("05:00 PM"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BranchOperatingHoursErrors.EmptyBranchId);
    }

    // =========================================================================
    // 3. State Update Invariants
    // =========================================================================

    [Theory]
    // Successful update from daytime to overnight shift
    [InlineData("06:00 PM", "03:00 AM", false, true, null)]
    // Failed update with identical open and close times
    [InlineData("10:00 AM", "10:00 AM", false, false, "BranchOperatingHours.SameOpenAndCloseTime")]
    public void Update_ShouldValidateAndUpdateCorrectly(
        string newOpen,
        string newClose,
        bool isClosed,
        bool expectedOvernight,
        string? expectedErrorCode)
    {
        // Arrange
        var operatingHours = BranchOperatingHours.Create(
            branchId: ValidBranchId,
            dayOfWeek: DayOfWeek.Wednesday,
            openTime: ParseTime("08:00 AM"),
            closeTime: ParseTime("04:00 PM"),
            isClosed: false).Value;

        // Act
        var updateResult = operatingHours.Update(
            openTime: ParseTime(newOpen),
            closeTime: ParseTime(newClose),
            isClosed: isClosed);

        // Assert
        if (expectedErrorCode is null)
        {
            updateResult.IsSuccess.Should().BeTrue();
            operatingHours.IsOvernight.Should().Be(expectedOvernight);
        }
        else
        {
            updateResult.IsFailure.Should().BeTrue();
            updateResult.Error.Code.Should().Be(expectedErrorCode);
        }
    }
}
