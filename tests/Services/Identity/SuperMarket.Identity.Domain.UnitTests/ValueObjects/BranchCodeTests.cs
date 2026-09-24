using FluentAssertions;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Domain.UnitTests.ValueObjects;

public sealed class BranchCodeTests
{
    // =========================================================================
    // 1. Data-Driven Validation & Normalization Matrix ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    // Happy Path: Normalization (Trim + Uppercase)
    [InlineData("   br-north-01   ", true, "BR-NORTH-01", null)]
    // Boundary Value: Min Length = 2 characters
    [InlineData("BR", true, "BR", null)]
    // Boundary Value: Max Length = 20 characters
    [InlineData("AAAAAAAAAAAAAAAAAAAA", true, "AAAAAAAAAAAAAAAAAAAA", null)]
    // Boundary Violation: Below Min Length (1 character)
    [InlineData("A", false, null, "Branch.InvalidBranchCodeLength")]
    // Boundary Violation: Above Max Length (21 characters)
    [InlineData("AAAAAAAAAAAAAAAAAAAAA", false, null, "Branch.InvalidBranchCodeLength")]
    // Empty & Whitespace Validation
    [InlineData(null, false, null, "Branch.EmptyBranchCode")]
    [InlineData("", false, null, "Branch.EmptyBranchCode")]
    [InlineData("   ", false, null, "Branch.EmptyBranchCode")]
    [InlineData("\t\n", false, null, "Branch.EmptyBranchCode")]
    public void Create_ShouldValidateAndNormalize_AcrossAllEquivalenceClassesAndBoundaries(
        string? input,
        bool expectedSuccess,
        string? expectedNormalizedValue,
        string? expectedErrorCode)
    {
        // Act
        var result = BranchCode.Create(input);

        // Assert
        if (expectedSuccess)
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be(expectedNormalizedValue);
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(expectedErrorCode);
        }
    }

    // =========================================================================
    // 2. Value Object Structural Equality (المقارنة بالقيم)
    // =========================================================================

    [Fact]
    public void ValueObject_EqualityAndConversion_ShouldBehaveCorrectly()
    {
        // Arrange
        var codeA1 = BranchCode.Create("br-01").Value;
        var codeA2 = BranchCode.Create("  BR-01  ").Value;
        var codeB = BranchCode.Create("BR-02").Value;

        // Assert Structural Equality
        codeA1.Should().Be(codeA2);
        (codeA1 == codeA2).Should().BeTrue();
        codeA1.GetHashCode().Should().Be(codeA2.GetHashCode());

        // Assert Inequality
        codeA1.Should().NotBe(codeB);
        (codeA1 != codeB).Should().BeTrue();

        // Assert Implicit string conversion
        string stringVal = codeA1;
        stringVal.Should().Be("BR-01");
        codeA1.ToString().Should().Be("BR-01");
    }
}
