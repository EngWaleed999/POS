using FluentAssertions;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.UnitTests.Results;

public class ResultTTests
{
    // -------------------------------------------------------------------------
    // 1. Success Value Access
    // -------------------------------------------------------------------------
    [Fact]
    public void Value_ShouldReturnExpectedValue_WhenResultIsSuccess()
    {
        // Arrange
        const decimal expectedPrice = 49.99m;

        // Act
        var result = Result.Success(expectedPrice);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedPrice);
        result.Error.Should().Be(Error.None);
    }

    // -------------------------------------------------------------------------
    // 2. Fail-Fast Guard on Value Access
    // -------------------------------------------------------------------------
    [Fact]
    public void Value_ShouldThrowInvalidOperationException_WhenResultIsFailure()
    {
        // Arrange
        var error = Error.NotFound("Shift.Closed", "Current cashier shift is closed.");
        var result = Result.Failure<decimal>(error);

        // Act
        Action act = () => _ = result.Value;

        // Assert: Accessing Value on failure must fail immediately (Fail-Fast)
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The value of a failure result cannot be accessed. Always check IsSuccess before reading Value.");
    }

    // -------------------------------------------------------------------------
    // 3. Implicit Operator Conversions
    // -------------------------------------------------------------------------
    [Fact]
    public void ImplicitOperator_ShouldConvertNonNullValue_ToSuccessResult()
    {
        // Arrange
        const string barcode = "628100123456";

        // Act: Implicit conversion from string to Result<string>
        Result<string> result = barcode;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(barcode);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertNullValue_ToFailureResultWithNullValueError()
    {
        // Arrange
        string? nullString = null;

        // Act: Implicit conversion from null to Result<string>
        Result<string> result = nullString;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(Error.NullValue);
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertError_ToFailureResult()
    {
        // Arrange
        var expectedError = Error.Conflict("Invoice.AlreadyPaid", "Invoice is already paid.");

        // Act: Implicit conversion from Error to Result<int>
        Result<int> result = expectedError;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(expectedError);
    }
}
