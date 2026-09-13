using System.Diagnostics;
using System.Diagnostics.Metrics;
using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.Application;

/// <summary>
/// MediatR pipeline behavior that provides:
/// 1. Structured Logging for command/query execution lifecycle.
/// 2. Performance Tracking with automated Slow-Request warnings (exceeding 500ms threshold).
/// 3. OpenTelemetry-compliant Metrics via <see cref="System.Diagnostics.Metrics.Meter"/> (Request Counter and Duration Histogram).
/// </summary>
public sealed class LoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Meter PosMeter = new("SuperMarket.POS.Core", "1.0.0");
    private static readonly Counter<long> RequestCounter = PosMeter.CreateCounter<long>(
        "pos_requests_total",
        "requests",
        "Total number of requests processed by the application pipeline");
    private static readonly Histogram<double> RequestDurationHistogram = PosMeter.CreateHistogram<double>(
        "pos_request_duration_ms",
        "ms",
        "Execution duration of application requests in milliseconds");

    private const int SlowRequestThresholdMs = 500;
    private readonly ILogger<LoggingPipelineBehavior<TRequest, TResponse>> _logger;

    public LoggingPipelineBehavior(ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Starting request: {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Track metrics
            RequestCounter.Add(1, new KeyValuePair<string, object?>("request_name", requestName));
            RequestDurationHistogram.Record(elapsedMs, new KeyValuePair<string, object?>("request_name", requestName));

            // Check if result is a business failure
            if (response is Result { IsFailure: true } failureResult)
            {
                _logger.LogWarning(
                    "Request {RequestName} failed with business error {ErrorCode}: {ErrorDescription} in {ElapsedMilliseconds}ms",
                    requestName,
                    failureResult.Error.Code,
                    failureResult.Error.Description,
                    elapsedMs);
            }
            else
            {
                _logger.LogInformation(
                    "Completed request {RequestName} successfully in {ElapsedMilliseconds}ms",
                    requestName,
                    elapsedMs);
            }

            // Slow request detection for POS latency SLA
            if (elapsedMs > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "SLOW REQUEST ALERT: {RequestName} exceeded threshold ({ThresholdMs}ms) with duration {ElapsedMilliseconds}ms",
                    requestName,
                    SlowRequestThresholdMs,
                    elapsedMs);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Request {RequestName} crashed after {ElapsedMilliseconds}ms due to an unhandled exception",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
