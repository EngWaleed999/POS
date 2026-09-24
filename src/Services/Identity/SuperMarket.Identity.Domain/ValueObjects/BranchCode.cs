// Value Object representing a standardized branch code (e.g., BR-01).

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.ValueObjects;

public sealed class BranchCode : ValueObject
{
    public const int MinLength = 2;
    public const int MaxLength = 20;

    public string Value { get; } = default!;

    // Required by EF Core
    private BranchCode()
    {
    }

    private BranchCode(string value)
    {
        Value = value;
    }

    public static Result<BranchCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<BranchCode>(BranchErrors.EmptyBranchCode);

        var trimmed = value.Trim().ToUpperInvariant();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
            return Result.Failure<BranchCode>(BranchErrors.InvalidBranchCodeLength);

        return Result.Success(new BranchCode(trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(BranchCode code) => code.Value;
}
