using FluentAssertions;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.UnitTests.Results;

public class ResultExtensionsTests
{
    // -------------------------------------------------------------------------
    // 1. Match (Non-Generic Result)
    // -------------------------------------------------------------------------
    [Fact]
    public void Match_ShouldExecuteOnSuccess_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var output = result.Match(
            onSuccess: () => "SUCCESS_PATH",
            onFailure: _ => "FAILURE_PATH");

        // Assert
        output.Should().Be("SUCCESS_PATH");
    }

    [Fact]
    public void Match_ShouldExecuteOnFailure_WhenResultIsFailure()
    {
        // Arrange
        var error = Error.Validation("Input.Invalid", "Invalid format.");
        var result = Result.Failure(error);

        // Act
        var output = result.Match(
            onSuccess: () => "SUCCESS_PATH",
            onFailure: err => $"ERROR:{err.Code}");

        // Assert
        output.Should().Be("ERROR:Input.Invalid");
    }

    [Fact]
    public void Match_ShouldThrowArgumentNullException_WhenDelegatesAreNull()
    {
        // Arrange
        var result = Result.Success();

        // Act & Assert
        Action actNullSuccess = () => result.Match(onSuccess: null!, onFailure: _ => "OK");
        Action actNullFailure = () => result.Match(onSuccess: () => "OK", onFailure: null!);

        actNullSuccess.Should().Throw<ArgumentNullException>();
        actNullFailure.Should().Throw<ArgumentNullException>();
    }

    // -------------------------------------------------------------------------
    // 2. Match (Generic Result<TValue>)
    // -------------------------------------------------------------------------
    [Fact]
    public void MatchGeneric_ShouldExecuteOnSuccessWithInnerValue_WhenResultIsSuccess()
    {
        // Arrange
        var result = Result.Success(100);

        // Act
        var output = result.Match(
            onSuccess: val => $"VALUE:{val * 2}",
            onFailure: _ => "ERROR");

        // Assert
        output.Should().Be("VALUE:200");
    }

    [Fact]
    public void MatchGeneric_ShouldExecuteOnFailureWithError_WhenResultIsFailure()
    {
        // Arrange
        var error = Error.NotFound("Cashier.NotFound", "Cashier does not exist.");
        var result = Result.Failure<int>(error);

        // Act
        var output = result.Match(
            onSuccess: val => $"VALUE:{val}",
            onFailure: err => $"ERROR:{err.Code}");

        // Assert
        output.Should().Be("ERROR:Cashier.NotFound");
    }

    // -------------------------------------------------------------------------
    // 3. Ensure (Conditional Assertion)
    // -------------------------------------------------------------------------
    [Fact]
    public void Ensure_ShouldReturnOriginalResult_WhenPredicateIsTrue()
    {
        // Arrange
        var result = Result.Success(50);
        var guardError = Error.Validation("Quantity.Invalid", "Quantity must be positive.");

        // Act
        var evaluated = result.Ensure(qty => qty > 0, guardError);

        // Assert
        evaluated.IsSuccess.Should().BeTrue();
        evaluated.Value.Should().Be(50);
    }

    [Fact]
    public void Ensure_ShouldReturnFailure_WhenPredicateIsFalse()
    {
        // Arrange
        var result = Result.Success(-10);
        var guardError = Error.Validation("Quantity.Invalid", "Quantity must be positive.");

        // Act
        var evaluated = result.Ensure(qty => qty > 0, guardError);

        // Assert
        evaluated.IsSuccess.Should().BeFalse();
        evaluated.Error.Should().Be(guardError);
    }

    [Fact]
    public void Ensure_ShouldShortCircuit_WhenOriginalResultIsAlreadyFailure()
    {
        // Arrange
        var originalError = Error.NotFound("Item.NotFound", "Item not found.");
        var result = Result.Failure<int>(originalError);
        var guardError = Error.Validation("Guard.Failed", "Guard failed.");
        var predicateWasExecuted = false;

        // Act
        var evaluated = result.Ensure(qty =>
        {
            predicateWasExecuted = true;
            return qty > 0;
        }, guardError);

        // Assert: Predicate must NOT even be invoked on a failed result
        predicateWasExecuted.Should().BeFalse();
        evaluated.IsSuccess.Should().BeFalse();
        evaluated.Error.Should().Be(originalError);
    }

    // -------------------------------------------------------------------------
    // 4. Map (Projection)
    // -------------------------------------------------------------------------
    [Fact]
    public void Map_ShouldTransformValue_WhenOriginalResultIsSuccess()
    {
        // Arrange
        var result = Result.Success(10);

        // Act: Map int (10) to string ("Result: 10")
        var mapped = result.Map(val => $"Result: {val}");

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("Result: 10");
    }

    [Fact]
    public void Map_ShouldShortCircuitAndReturnOriginalError_WhenOriginalResultIsFailure()
    {
        // Arrange
        var error = Error.Failure("Calc.Error", "Calculation failure.");
        var result = Result.Failure<int>(error);
        var mapperWasExecuted = false;

        // Act
        var mapped = result.Map(val =>
        {
            mapperWasExecuted = true;
            return val.ToString();
        });

        // Assert
        mapperWasExecuted.Should().BeFalse();
        mapped.IsSuccess.Should().BeFalse();
        mapped.Error.Should().Be(error);
    }

    // -------------------------------------------------------------------------
    // 5. Bind (Chaining Dependent Operations)
    // -------------------------------------------------------------------------
    [Fact]
    public void Bind_ShouldChainNextOperation_WhenOriginalResultIsSuccess()
    {
        // Arrange: Step 1 succeeds with barcode
        var step1 = Result.Success("628100123456");

        // Act: Step 2 looks up price and returns Result<decimal>
        var chained = step1.Bind(barcode => Result.Success(25.50m));

        // Assert
        chained.IsSuccess.Should().BeTrue();
        chained.Value.Should().Be(25.50m);
    }

    [Fact]
    public void Bind_ShouldShortCircuitAndNotExecuteBinder_WhenOriginalResultIsFailure()
    {
        // Arrange: Step 1 fails
        var originalError = Error.Validation("Barcode.Invalid", "Barcode is invalid.");
        var step1 = Result.Failure<string>(originalError);
        var binderWasExecuted = false;

        // Act
        var chained = step1.Bind(barcode =>
        {
            binderWasExecuted = true;
            return Result.Success(25.50m);
        });

        // Assert
        binderWasExecuted.Should().BeFalse();
        chained.IsSuccess.Should().BeFalse();
        chained.Error.Should().Be(originalError);
    }
}
