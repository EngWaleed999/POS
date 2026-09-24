// Structured validation error carrying property-specific failure details for RFC 7807 ProblemDetails.

namespace SuperMarket.BuildingBlocks.Results;

public sealed record ValidationError : Error
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationError(
        string code,
        string description,
        IReadOnlyDictionary<string, string[]> errors)
        : base(code, description, ErrorType.Validation)
    {
        Errors = errors;
    }
}
