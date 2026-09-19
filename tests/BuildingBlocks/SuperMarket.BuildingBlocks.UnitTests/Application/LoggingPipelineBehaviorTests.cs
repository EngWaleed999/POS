using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="LoggingPipelineBehavior{TRequest, TResponse}"/>
/// validating lifecycle logging, business failure warnings, and unhandled exception logging.
/// </summary>
public class LoggingPipelineBehaviorTests
{
    // =========================================================================
    // 1. Successful Request Lifecycle Logging
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldLogStartAndCompletion_WhenRequestSucceeds()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<LoggingPipelineBehavior<ProcessPaymentCommand, Result>>>();
        var behavior = new LoggingPipelineBehavior<ProcessPaymentCommand, Result>(loggerMock.Object);

        var command = new ProcessPaymentCommand(500m);
        RequestHandlerDelegate<Result> next = () => Task.FromResult(Result.Success());

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify Start Log (Information)
        VerifyLogger(loggerMock, LogLevel.Information, "Starting request", Times.Once());

        // Verify Success Log (Information)
        VerifyLogger(loggerMock, LogLevel.Information, "Completed request", Times.Once());

        // Verify no warnings or errors were logged
        VerifyLogger(loggerMock, LogLevel.Warning, times: Times.Never());
        VerifyLogger(loggerMock, LogLevel.Error, times: Times.Never());
    }

    // =========================================================================
    // 2. Business Failure Result Logging (Warning Level)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldLogWarning_WhenRequestReturnsBusinessFailure()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<LoggingPipelineBehavior<ProcessPaymentCommand, Result>>>();
        var behavior = new LoggingPipelineBehavior<ProcessPaymentCommand, Result>(loggerMock.Object);

        var command = new ProcessPaymentCommand(10_000m);
        var businessError = Error.Conflict("Payment.LimitExceeded", "Transaction exceeds maximum allowed limit");
        RequestHandlerDelegate<Result> next = () => Task.FromResult(Result.Failure(businessError));

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.LimitExceeded");

        // Verify Warning Log was emitted with error details
        VerifyLogger(loggerMock, LogLevel.Warning, "Payment.LimitExceeded", Times.Once());

        // Completion log at Information level should NOT be logged when failed
        VerifyLogger(loggerMock, LogLevel.Information, "Completed request", Times.Never());
    }

    // =========================================================================
    // 3. Unhandled Exception Logging & Re-throw
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldLogErrorAndRethrow_WhenUnhandledExceptionOccurs()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<LoggingPipelineBehavior<ProcessPaymentCommand, Result>>>();
        var behavior = new LoggingPipelineBehavior<ProcessPaymentCommand, Result>(loggerMock.Object);

        var command = new ProcessPaymentCommand(200m);
        var expectedException = new InvalidOperationException("Payment Gateway connection timed out");
        RequestHandlerDelegate<Result> next = () => throw expectedException;

        // Act
        Func<Task> act = async () => await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        var thrown = await act.Should().ThrowExactlyAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Be("Payment Gateway connection timed out");

        // Verify Error was logged with the exact exception
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("crashed due to an unhandled exception")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "pipeline must log the unhandled exception before letting it bubble up to the global exception handler");
    }

    // =========================================================================
    // Helper: Moq ILogger Verifier
    // =========================================================================

    private static void VerifyLogger<T>(
        Mock<ILogger<T>> mock,
        LogLevel level,
        string? messageSubstring = null,
        Times? times = null)
    {
        var expectedTimes = times ?? Times.Once();

        mock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => messageSubstring == null || v.ToString()!.Contains(messageSubstring)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            expectedTimes);
    }

    // =========================================================================
    // Test Fakes
    // =========================================================================

    public record ProcessPaymentCommand(decimal Amount) : IRequest<Result>;
}
