namespace SuperMarket.BuildingBlocks.Results;

public static class ResultExtensions
{
  
    public static TOutput Match<TOutput>(
        this Result result,
        Func<TOutput> onSuccess,
        Func<Error, TOutput> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess ? onSuccess() : onFailure(result.Error);
    }

 
    public static TOutput Match<TValue, TOutput>(
        this Result<TValue> result,
        Func<TValue, TOutput> onSuccess,
        Func<Error, TOutput> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
    }


    public static Result<TValue> Ensure<TValue>(
        this Result<TValue> result,
        Func<TValue, bool> predicate,
        Error error)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value) ? result : Result.Failure<TValue>(error);
    }

    /// <summary>
    /// Projects the value of a successful result into a new form.
    /// </summary>
    public static Result<TOutput> Map<TValue, TOutput>(
        this Result<TValue> result,
        Func<TValue, TOutput> mapper)
    {
        if (result.IsFailure)
            return Result.Failure<TOutput>(result.Error);

        return Result.Success(mapper(result.Value));
    }

    /// <summary>
    /// Chains a subsequent operation that itself returns a <see cref="Result{TOutput}"/>.
    /// </summary>
    public static Result<TOutput> Bind<TValue, TOutput>(
        this Result<TValue> result,
        Func<TValue, Result<TOutput>> binder)
    {
        if (result.IsFailure)
            return Result.Failure<TOutput>(result.Error);

        return binder(result.Value);
    }
}
