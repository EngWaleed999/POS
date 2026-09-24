// Value Object representing a standardized POS terminal/register code (e.g., REG-01, POS-01).

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.ValueObjects;

public sealed class RegisterCode : ValueObject
{
    public const int MinLength = 2;
    public const int MaxLength = 20;

    public string Value { get; } = default!;

    // Required by EF Core
    private RegisterCode()
    {
    }

    private RegisterCode(string value)
    {
        Value = value;
    }

    public static Result<RegisterCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<RegisterCode>(POSRegisterErrors.EmptyRegisterCode);

        var trimmed = value.Trim().ToUpperInvariant();

        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
            return Result.Failure<RegisterCode>(POSRegisterErrors.InvalidRegisterCodeLength);

        return Result.Success(new RegisterCode(trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(RegisterCode code) => code.Value;
}
