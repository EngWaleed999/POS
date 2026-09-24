// Value Object representing a physical branch address flattened via EF Core OwnsOne.

using SuperMarket.BuildingBlocks.Domain;
using SuperMarket.BuildingBlocks.Results;
using SuperMarket.Identity.Domain.Errors;

namespace SuperMarket.Identity.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; } = default!;
    public string City { get; } = default!;
    public string Region { get; } = default!;
    public string? PostalCode { get; }

    // Required by EF Core
    private Address()
    {
    }

    private Address(string street, string city, string region, string? postalCode)
    {
        Street = street;
        City = city;
        Region = region;
        PostalCode = postalCode;
    }

    public static Result<Address> Create(string street, string city, string region, string? postalCode = null)
    {
        if (string.IsNullOrWhiteSpace(street))
            return Result.Failure<Address>(BranchErrors.EmptyStreet);

        if (string.IsNullOrWhiteSpace(city))
            return Result.Failure<Address>(BranchErrors.EmptyCity);

        if (string.IsNullOrWhiteSpace(region))
            return Result.Failure<Address>(BranchErrors.EmptyRegion);

        return Result.Success(new Address(
            street.Trim(),
            city.Trim(),
            region.Trim(),
            string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return Region;
        yield return PostalCode;
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(PostalCode)
            ? $"{Street}, {City}, {Region}"
            : $"{Street}, {City}, {Region} - {PostalCode}";
}
