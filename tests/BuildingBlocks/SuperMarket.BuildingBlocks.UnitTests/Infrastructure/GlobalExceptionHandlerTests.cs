using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SuperMarket.BuildingBlocks.Infrastructure;

namespace SuperMarket.BuildingBlocks.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="GlobalExceptionHandler"/>
/// validating RFC 7807 problem details generation, trace ID inclusion,
/// 500 status code setting, and diagnostic error logging.
/// </summary>
public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ShouldSetStatusCode500AndWriteProblemDetails_WhenExceptionOccurs()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        var problemDetailsMock = new Mock<IProblemDetailsService>();

        ProblemDetailsContext? capturedContext = null;
        problemDetailsMock
            .Setup(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedContext = ctx)
            .ReturnsAsync(true);

        var handler = new GlobalExceptionHandler(loggerMock.Object, problemDetailsMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/orders/checkout";
        httpContext.TraceIdentifier = "TRACE-REQUEST-12345";

        var exception = new InvalidOperationException("Fatal database deadlock detected");

        // Act
        var result = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        capturedContext.Should().NotBeNull();
        var details = capturedContext!.ProblemDetails;
        details.Status.Should().Be(StatusCodes.Status500InternalServerError);
        details.Title.Should().Be("Internal Server Error");
        details.Instance.Should().Be("/api/v1/orders/checkout");
        details.Extensions.Should().ContainKey("traceId");
        details.Extensions["traceId"].Should().Be("TRACE-REQUEST-12345");

        // Verify Error level logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unhandled exception occurred") && v.ToString()!.Contains("/api/v1/orders/checkout")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
