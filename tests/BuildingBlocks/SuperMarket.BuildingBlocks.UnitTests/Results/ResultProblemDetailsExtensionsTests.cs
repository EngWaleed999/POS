using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.UnitTests.Results;

/// <summary>
/// Unit tests for <see cref="ResultProblemDetailsExtensions"/>
/// validating mapping of Domain errors to RFC 7807 ProblemDetails HTTP responses.
/// </summary>
public class ResultProblemDetailsExtensionsTests
{
    // =========================================================================
    // 1. Parameterized Status Code Mapping ([Theory] with [InlineData])
    // =========================================================================

    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest, "Validation Error")]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound, "Resource Not Found")]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict, "Conflict")]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized, "Unauthorized")]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden, "Forbidden")]
    public void ToProblemDetails_ShouldMapErrorTypeToCorrectStatusCodeAndTitle(
        ErrorType errorType, int expectedStatusCode, string expectedTitle)
    {
        // Arrange
        var error = errorType switch
        {
            ErrorType.Validation => Error.Validation("TEST.CODE", "Detailed failure message"),
            ErrorType.NotFound => Error.NotFound("TEST.CODE", "Detailed failure message"),
            ErrorType.Conflict => Error.Conflict("TEST.CODE", "Detailed failure message"),
            ErrorType.Unauthorized => Error.Unauthorized("TEST.CODE", "Detailed failure message"),
            ErrorType.Forbidden => Error.Forbidden("TEST.CODE", "Detailed failure message"),
            _ => Error.Failure("TEST.CODE", "Detailed failure message")
        };
        var result = Result.Failure(error);

        // Act
        var httpResult = result.ToProblemDetails();

        // Assert
        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(expectedStatusCode);
        problemResult.ProblemDetails.Status.Should().Be(expectedStatusCode);
        problemResult.ProblemDetails.Title.Should().Be(expectedTitle);
        problemResult.ProblemDetails.Detail.Should().Be("Detailed failure message");
        problemResult.ProblemDetails.Extensions.Should().ContainKey("errorCode");
        problemResult.ProblemDetails.Extensions["errorCode"].Should().Be("TEST.CODE");
    }

    // =========================================================================
    // 2. Generic Result<T> Extension
    // =========================================================================

    [Fact]
    public void ToProblemDetailsGeneric_ShouldMapErrorCorrectly_WhenResultTIsFailure()
    {
        // Arrange
        var error = Error.NotFound("Product.NotFound", "The product with specified ID does not exist");
        var result = Result.Failure<string>(error);

        // Act
        var httpResult = result.ToProblemDetails();

        // Assert
        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problemResult.ProblemDetails.Title.Should().Be("Resource Not Found");
        problemResult.ProblemDetails.Detail.Should().Be("The product with specified ID does not exist");
        problemResult.ProblemDetails.Extensions["errorCode"].Should().Be("Product.NotFound");
    }

    // =========================================================================
    // 3. Invariant Guards: Forbid Converting Success to ProblemDetails
    // =========================================================================

    [Fact]
    public void ToProblemDetails_ShouldThrowInvalidOperationException_WhenResultIsSuccess()
    {
        // Arrange
        var successResult = Result.Success();

        // Act
        Action act = () => successResult.ToProblemDetails();

        // Assert
        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("Cannot convert a successful result into ProblemDetails.");
    }

    [Fact]
    public void ToProblemDetailsGeneric_ShouldThrowInvalidOperationException_WhenResultTIsSuccess()
    {
        // Arrange
        var successResult = Result.Success(42);

        // Act
        Action act = () => successResult.ToProblemDetails();

        // Assert
        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("Cannot convert a successful result into ProblemDetails.");
    }

    // =========================================================================
    // 4. Structured ValidationError Mapping (RFC 7807 ValidationProblemDetails)
    // =========================================================================

    [Fact]
    public void ToProblemDetails_ShouldReturnValidationProblem_WhenErrorIsValidationError()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["Username"] = new[] { "Username cannot be empty." },
            ["Email"] = new[] { "Invalid email address." }
        };
        var validationError = Error.Validation("General.Validation", "Validation failed", errors);
        var result = Result.Failure(validationError);

        // Act
        var httpResult = result.ToProblemDetails();

        // Assert
        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemResult.ProblemDetails.Should().BeOfType<HttpValidationProblemDetails>();
        var validationProblemDetails = (HttpValidationProblemDetails)problemResult.ProblemDetails;
        validationProblemDetails.Errors.Should().ContainKey("Username");
        validationProblemDetails.Errors["Username"].Should().Contain("Username cannot be empty.");
        validationProblemDetails.Errors.Should().ContainKey("Email");
        validationProblemDetails.Errors["Email"].Should().Contain("Invalid email address.");
        validationProblemDetails.Extensions.Should().ContainKey("errorCode");
        validationProblemDetails.Extensions["errorCode"].Should().Be("General.Validation");
    }
}

