using FluentAssertions;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Domain.UnitTests.ValueObjects;

public sealed class AddressTests
{
    // =========================================================================
    // 1. Data-Driven Validation Matrix ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    // Empty Street
    [InlineData(null, "Riyadh", "Central", "Branch.EmptyStreet")]
    [InlineData("", "Riyadh", "Central", "Branch.EmptyStreet")]
    [InlineData("   ", "Riyadh", "Central", "Branch.EmptyStreet")]
    // Empty City
    [InlineData("Main St", null, "Central", "Branch.EmptyCity")]
    [InlineData("Main St", "", "Central", "Branch.EmptyCity")]
    [InlineData("Main St", "   ", "Central", "Branch.EmptyCity")]
    // Empty Region
    [InlineData("Main St", "Riyadh", null, "Branch.EmptyRegion")]
    [InlineData("Main St", "Riyadh", "", "Branch.EmptyRegion")]
    [InlineData("Main St", "Riyadh", "   ", "Branch.EmptyRegion")]
    public void Create_ShouldFail_WhenRequiredAddressFieldsAreMissing(
        string? street,
        string? city,
        string? region,
        string expectedErrorCode)
    {
        // Act
        var result = Address.Create(street!, city!, region!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedErrorCode);
    }

    [Theory]
    [InlineData("12345", "12345", "King Fahd Rd, Riyadh, Central - 12345")]
    [InlineData(null, null, "King Fahd Rd, Riyadh, Central")]
    [InlineData("", null, "King Fahd Rd, Riyadh, Central")]
    [InlineData("   ", null, "King Fahd Rd, Riyadh, Central")]
    public void Create_ShouldHandlePostalCodeAndFormatToStringCorrectly(
        string? inputPostalCode,
        string? expectedPostalCode,
        string expectedToString)
    {
        // Act
        var result = Address.Create("King Fahd Rd", "Riyadh", "Central", inputPostalCode);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PostalCode.Should().Be(expectedPostalCode);
        result.Value.ToString().Should().Be(expectedToString);
    }

    // =========================================================================
    // 2. Value Object Structural Equality
    // =========================================================================

    [Fact]
    public void Equals_ShouldEvaluateStructuralEqualityCorrectly()
    {
        // Arrange
        var addr1 = Address.Create("Street 1", "Riyadh", "Central", "11111").Value;
        var addr2 = Address.Create("  Street 1  ", "  Riyadh  ", "  Central  ", "  11111  ").Value;
        var addrDifferent = Address.Create("Street 2", "Jeddah", "Western", "22222").Value;

        // Assert
        addr1.Should().Be(addr2);
        (addr1 == addr2).Should().BeTrue();
        addr1.GetHashCode().Should().Be(addr2.GetHashCode());

        addr1.Should().NotBe(addrDifferent);
        (addr1 != addrDifferent).Should().BeTrue();
    }
}
