using FluentAssertions;
using SuperMarket.Identity.Domain.ValueObjects;

namespace SuperMarket.Identity.Domain.UnitTests.ValueObjects;

public sealed class RegisterCodeTests
{
    // =========================================================================
    // 1. Data-Driven Validation & Normalization Matrix ([Theory] + [InlineData])
    // =========================================================================

    [Theory]
    // Happy Path: Normalization (Trim + Uppercase)
    [InlineData("   pos-cashier-01   ", true, "POS-CASHIER-01", null)]
    // Boundary Value: Min Length = 2 characters
    [InlineData("R1", true, "R1", null)]
    // Boundary Value: Max Length = 20 characters
    [InlineData("PPPPPPPPPPPPPPPPPPPP", true, "PPPPPPPPPPPPPPPPPPPP", null)]
    // Boundary Violation: Below Min Length (1 character)
    [InlineData("P", false, null, "POSRegister.InvalidRegisterCodeLength")]
    // Boundary Violation: Above Max Length (21 characters)
    [InlineData("PPPPPPPPPPPPPPPPPPPPP", false, null, "POSRegister.InvalidRegisterCodeLength")]
    // Empty & Whitespace Validation
    [InlineData(null, false, null, "POSRegister.EmptyRegisterCode")]
    [InlineData("", false, null, "POSRegister.EmptyRegisterCode")]
    [InlineData("   ", false, null, "POSRegister.EmptyRegisterCode")]
    [InlineData("\t\n", false, null, "POSRegister.EmptyRegisterCode")]
    public void Create_ShouldValidateAndNormalize_AcrossAllEquivalenceClassesAndBoundaries(
        string? input,
        bool expectedSuccess,
        string? expectedNormalizedValue,
        string? expectedErrorCode)
    {
        // Act
        var result = RegisterCode.Create(input);

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
        var codeA1 = RegisterCode.Create("pos-01").Value;
        var codeA2 = RegisterCode.Create("  POS-01  ").Value;
        var codeB = RegisterCode.Create("POS-02").Value;

        // Assert Structural Equality
        codeA1.Should().Be(codeA2);
        (codeA1 == codeA2).Should().BeTrue();
        codeA1.GetHashCode().Should().Be(codeA2.GetHashCode());

        // Assert Inequality
        codeA1.Should().NotBe(codeB);
        (codeA1 != codeB).Should().BeTrue();

        // Assert Implicit string conversion
        string stringVal = codeA1;
        stringVal.Should().Be("POS-01");
        codeA1.ToString().Should().Be("POS-01");
    }
}
