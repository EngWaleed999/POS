using FluentAssertions;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.UnitTests.Results;

public class ResultTests
{
    // -------------------------------------------------------------------------
    // 1. Invariant Guard Tests
    // -------------------------------------------------------------------------
    [Fact]
    public void Constructor_ShouldThrowInvalidOperationException_WhenSuccessInitializedWithNonEmptyError()
    {
        // Arrange
        var error = Error.Failure("Order.Invalid", "Something went wrong.");

        // Act
        Action act = () => new TestResult(isSuccess: true, error: error);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A successful result cannot be initialized with an error.");
    }

    [Fact]
    public void Constructor_ShouldThrowInvalidOperationException_WhenFailureInitializedWithNoError()
    {
        // Arrange & Act
        Action act = () => new TestResult(isSuccess: false, error: Error.None);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A failure result must be initialized with a non-empty error.");
    }

    // -------------------------------------------------------------------------
    // 2. Factory Methods & States
    // -------------------------------------------------------------------------
    [Fact]
    public void Success_ShouldCreateResult_WithIsSuccessTrueAndErrorNone()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ShouldCreateResult_WithIsSuccessFalseAndSpecifiedError()
    {
        // Arrange
        var expectedError = Error.NotFound("Product.NotFound", "Product was not found.");

        // Act
        var result = Result.Failure(expectedError);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
    }

    [Fact]
    public void Create_ShouldReturnSuccess_WhenValueIsNotNull()
    {
        // Arrange
        const string value = "POS-Register-01";

        // Act
        var result = Result.Create(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Create_ShouldReturnFailure_WhenValueIsNull()
    {
        // Arrange
        string? value = null;

        // Act
        var result = Result.Create(value);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(Error.NullValue);
    }

    // -------------------------------------------------------------------------
    // Test Helper (Exposes Protected Internal Constructor)
    // -------------------------------------------------------------------------
    private class TestResult : Result
    {
        public TestResult(bool isSuccess, Error error) : base(isSuccess, error)
        {
        }
    }
}
