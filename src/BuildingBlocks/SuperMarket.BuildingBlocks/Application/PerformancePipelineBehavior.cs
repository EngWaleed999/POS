using System.Diagnostics;
using System.Diagnostics.Metrics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SuperMarket.BuildingBlocks.Application;

public sealed class PerformancePipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // -------------------------------------------------------------------------
    // OpenTelemetry Metrics Instrumentation
    // -------------------------------------------------------------------------
    private static readonly Meter PosMeter = new("SuperMarket.POS.Core", "1.0.0");

    private static readonly Counter<long> RequestCounter = PosMeter.CreateCounter<long>(
        "pos_requests_total",
        "requests",
        "Total number of requests processed by the application pipeline");

    private static readonly Histogram<double> RequestDurationHistogram = PosMeter.CreateHistogram<double>(
        "pos_request_duration_ms",
        "ms",
        "Execution duration of application requests in milliseconds");

    // -------------------------------------------------------------------------
    // Configuration & Dependencies
    // -------------------------------------------------------------------------
    private readonly int _slowRequestThresholdMs;
    private readonly ILogger<PerformancePipelineBehavior<TRequest, TResponse>> _logger;

    public PerformancePipelineBehavior(
        ILogger<PerformancePipelineBehavior<TRequest, TResponse>> logger,
        IOptions<PerformanceSettings>? options = null)
    {
        _logger = logger;
        _slowRequestThresholdMs = options?.Value.SlowRequestThresholdMs is > 0
            ? options.Value.SlowRequestThresholdMs
            : 500;
    }

    // -------------------------------------------------------------------------
    // Pipeline Execution & Performance Monitoring
    // -------------------------------------------------------------------------
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return await next();
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            RequestCounter.Add(1, new KeyValuePair<string, object?>("request_name", requestName));
            RequestDurationHistogram.Record(elapsedMs, new KeyValuePair<string, object?>("request_name", requestName));

            if (elapsedMs > _slowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "SLOW REQUEST ALERT: {RequestName} exceeded threshold ({ThresholdMs}ms) with duration {ElapsedMilliseconds}ms",
                    requestName,
                    _slowRequestThresholdMs,
                    elapsedMs);
            }
        }
    }
}
