using System.Reflection;
using FluentValidation;
using MediatR;
using SuperMarket.BuildingBlocks.Results;

namespace SuperMarket.BuildingBlocks.Application;

/// <summary>
/// MediatR pipeline behavior that automatically validates incoming requests using registered <see cref="IValidator{TRequest}"/> instances.
/// If validation fails, short-circuits execution and returns an appropriate <see cref="Result"/> or <see cref="Result{TValue}"/>
/// without invoking the command handler.
/// </summary>
/// <typeparam name="TRequest">The incoming command or query type.</typeparam>
/// <typeparam name="TResponse">The expected response type, constrained to Result types.</typeparam>
public sealed class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        // Format combined validation error message
        var errorMessage = string.Join(" | ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
        var validationError = Error.Validation("General.Validation", errorMessage);

        return CreateValidationResult(validationError);
    }

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
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Error))
                .MakeGenericMethod(valueType);

            return (TResponse)failureMethod.Invoke(null, [error])!;
        }

        throw new InvalidOperationException($"The return type '{typeof(TResponse).Name}' is not a supported Result type in ValidationPipelineBehavior.");
    }
}
