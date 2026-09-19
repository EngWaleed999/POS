using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SuperMarket.BuildingBlocks.Application;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="PerformancePipelineBehavior{TRequest, TResponse}"/>
/// validating stopwatch execution timing, slow-request warning thresholds,
/// and options fallback safety.
/// </summary>
public class PerformancePipelineBehaviorTests
{
    // =========================================================================
    // 1. Fast Request (Execution Duration Under Threshold)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldNotEmitWarning_WhenRequestCompletesUnderThreshold()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>>>();
        var options = Options.Create(new PerformanceSettings { SlowRequestThresholdMs = 1000 });
        var behavior = new PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>(loggerMock.Object, options);

        var query = new GenerateSalesReportQuery(DateTime.UtcNow);
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("Report Generated"));

        // Act
        var result = await behavior.Handle(query, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Report Generated");

        // Verify NO warning was logged
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "fast requests within threshold must not trigger warning logs");
    }

    // =========================================================================
    // 2. Slow Request (Execution Duration Exceeds Configured Threshold)
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldLogWarningAlert_WhenRequestDurationExceedsThreshold()
    {
        // Arrange: Set a very low threshold (20ms) and delay next by 60ms to trigger alert
        var loggerMock = new Mock<ILogger<PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>>>();
        var options = Options.Create(new PerformanceSettings { SlowRequestThresholdMs = 20 });
        var behavior = new PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>(loggerMock.Object, options);

        var query = new GenerateSalesReportQuery(DateTime.UtcNow);
        RequestHandlerDelegate<Result<string>> next = async () =>
        {
            await Task.Delay(60);
            return Result.Success("Heavy Report");
        };

        // Act
        var result = await behavior.Handle(query, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify Warning Log was emitted with "SLOW REQUEST ALERT"
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SLOW REQUEST ALERT") && v.ToString()!.Contains("GenerateSalesReportQuery")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "pipeline must alert operations team when a query/command violates SLA threshold");
    }

    // =========================================================================
    // 3. Fallback to Default Threshold when Options are Null or Invalid
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Handle_ShouldFallbackToDefault500msThreshold_WhenOptionsAreNullOrInvalid(int? customThreshold)
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>>>();
        IOptions<PerformanceSettings>? options = customThreshold.HasValue
            ? Options.Create(new PerformanceSettings { SlowRequestThresholdMs = customThreshold.Value })
            : null;

        var behavior = new PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>(loggerMock.Object, options);

        var query = new GenerateSalesReportQuery(DateTime.UtcNow);
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result.Success("Default Handled"));

        // Act
        var result = await behavior.Handle(query, next, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    // =========================================================================
    // 4. Guaranteed Finally Execution on Exception
    // =========================================================================

    [Fact]
    public async Task Handle_ShouldPropagateException_WhileStillExecutingFinallyBlock()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>>>();
        var behavior = new PerformancePipelineBehavior<GenerateSalesReportQuery, Result<string>>(loggerMock.Object);

        var query = new GenerateSalesReportQuery(DateTime.UtcNow);
        var expectedEx = new TimeoutException("Database connection timeout");
        RequestHandlerDelegate<Result<string>> next = () => throw expectedEx;

        // Act
        Func<Task> act = async () => await behavior.Handle(query, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowExactlyAsync<TimeoutException>()
            .WithMessage("Database connection timeout");
    }

    // =========================================================================
    // Test Fakes
    // =========================================================================

    public record GenerateSalesReportQuery(DateTime Date) : IRequest<Result<string>>;
}
