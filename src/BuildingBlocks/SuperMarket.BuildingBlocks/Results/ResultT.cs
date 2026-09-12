using System.Diagnostics.CodeAnalysis;

namespace SuperMarket.BuildingBlocks.Results;


public class Result<TValue> : Result
{
    private readonly TValue? _value;


    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }


    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result cannot be accessed. Always check IsSuccess before reading Value.");


    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
