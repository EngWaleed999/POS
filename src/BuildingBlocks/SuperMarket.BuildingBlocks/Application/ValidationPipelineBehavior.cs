// MediatR pipeline behavior that validates incoming commands/queries via FluentValidation.
// On failure, throws ValidationException — the API Global Exception Handler maps it to RFC 7807 ValidationProblemDetails (HTTP 400).

using FluentValidation;
using MediatR;

namespace SuperMarket.BuildingBlocks.Application;

public sealed class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    // -------------------------------------------------------------------------
    // Pipeline Execution Gate
    // -------------------------------------------------------------------------
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        throw new ValidationException(failures);
    }
}
