// MediatR pipeline behavior that validates incoming commands/queries via FluentValidation.
// On failure, short-circuits execution and returns a failure Result without throwing exceptions.

using System.Reflection;
using FluentValidation;
using MediatR;
using SuperMarket.BuildingBlocks.Results;

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

        // Execute validators sequentially to prevent DbContext concurrency violations
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (result.Errors is { Count: > 0 })
            {
                failures.AddRange(result.Errors.Where(f => f is not null));
            }
        }

        if (failures.Count == 0)
            return await next();

        var errorsDictionary = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());

        var errorMessage = string.Join(" | ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
        var validationError = Error.Validation("General.Validation", errorMessage, errorsDictionary);

        return CreateValidationResult(validationError);
    }

    // -------------------------------------------------------------------------
    // Result Factory Helper (Builds Result or Result<T> on Failure)
    // -------------------------------------------------------------------------
    private static TResponse CreateValidationResult(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        var resultType = typeof(TResponse);
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = resultType.GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(Result.Failure)
                            && m.IsGenericMethod
                            && m.GetParameters().Length == 1
                            && m.GetParameters()[0].ParameterType == typeof(Error))
                .MakeGenericMethod(valueType);

            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new InvalidOperationException(
            $"The return type '{typeof(TResponse).Name}' is not a supported Result type in ValidationPipelineBehavior.");
    }
}
