using MediatR;
using Microsoft.Extensions.Logging;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.Application;

public sealed class LoggingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // -------------------------------------------------------------------------
    // Dependencies & Injected Services
    // -------------------------------------------------------------------------
    private readonly ILogger<LoggingPipelineBehavior<TRequest, TResponse>> _logger;

    public LoggingPipelineBehavior(ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Pipeline Execution & Lifecycle Logging
    // -------------------------------------------------------------------------
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Starting request: {RequestName}", requestName);

        try
        {
            var response = await next();

            if (response is Result { IsFailure: true } failureResult)
            {
                _logger.LogWarning(
                    "Request {RequestName} failed with business error {ErrorCode}: {ErrorDescription}",
                    requestName,
                    failureResult.Error.Code,
                    failureResult.Error.Description);
            }
            else
            {
                _logger.LogInformation(
                    "Completed request {RequestName} successfully",
                    requestName);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Request {RequestName} crashed due to an unhandled exception",
                requestName);

            throw;
        }
    }
}
