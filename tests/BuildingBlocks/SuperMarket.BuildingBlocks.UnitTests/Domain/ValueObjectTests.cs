using FluentAssertions;
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.UnitTests.Domain;

public class ValueObjectTests
{
    // -------------------------------------------------------------------------
    // 1. Parameterized Component Equality ([Theory] with [InlineData])
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData(100, "SAR", 100, "SAR", true)]
    [InlineData(100, "SAR", 200, "SAR", false)]
    [InlineData(100, "SAR", 100, "USD", false)]
    [InlineData(0, "USD", 0, "USD", true)]
    [InlineData(-50, "EUR", -50, "EUR", true)]
    [InlineData(-50, "EUR", -50, "SAR", false)]
    public void Equality_ShouldBeEvaluatedCorrectly_BasedOnInternalComponents(
        decimal amount1, string currency1, decimal amount2, string currency2, bool expectedEqual)
    {
        // Arrange
        var money1 = new Money(amount1, currency1);
        var money2 = new Money(amount2, currency2);

        // Act & Assert
        money1.Equals(money2).Should().Be(expectedEqual);
        (money1 == money2).Should().Be(expectedEqual);
        (money1 != money2).Should().Be(!expectedEqual);

        if (expectedEqual)
        {
            money1.GetHashCode().Should().Be(money2.GetHashCode());
        }
    }

    // -------------------------------------------------------------------------
    // 2. Null Comparisons Safety
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparedWithNull()
    {
        // Arrange
        var money = new Money(100, "SAR");

        // Act & Assert
        money.Equals(null).Should().BeFalse();
        (money == null).Should().BeFalse();
        (null == money).Should().BeFalse();
        (money != null).Should().BeTrue();
        (null != money).Should().BeTrue();
    }

    [Fact]
    public void EqualityOperator_ShouldReturnTrue_WhenBothAreNull()
    {
        // Arrange
        Money? left = null;
        Money? right = null;

        // Act & Assert
        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingSameMemoryReferenceDirectly()
    {
        // Arrange
        var money = new Money(100, "SAR");
        var sameRef = money;

        // Act & Assert (triggers ReferenceEquals short-circuit)
        money.Equals(sameRef).Should().BeTrue();
    }

    [Fact]
    public void EqualsObjectOverload_ShouldHandlePolymorphicCastsAndNullCorrectly()
    {
        // Arrange
        var money = new Money(100, "SAR");
        object sameRef = money;
        object equalMoney = new Money(100, "SAR");
        object differentMoney = new Money(200, "SAR");
        object? nullObj = null;
        object nonValueObject = "just a string";

        // Act & Assert (invokes public override bool Equals(object? obj))
        money.Equals(sameRef).Should().BeTrue();
        money.Equals(equalMoney).Should().BeTrue();
        money.Equals(differentMoney).Should().BeFalse();
        money.Equals(nullObj).Should().BeFalse();
        money.Equals(nonValueObject).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 3. Nullable Internal Components ([Theory] with [InlineData])
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("King Fahd Road", null, "King Fahd Road", null, true)]
    [InlineData("King Fahd Road", "Apt 4B", "King Fahd Road", "Apt 4B", true)]
    [InlineData("King Fahd Road", null, "King Fahd Road", "Apt 4B", false)]
    [InlineData("King Fahd Road", "Apt 4B", "King Fahd Road", null, false)]
    [InlineData("King Fahd Road", null, "Olaya Street", null, false)]
    [InlineData("King Fahd Road", "Apt 4B", "Olaya Street", "Apt 4B", false)]
    public void ValueObjects_WithNullableComponents_ShouldEvaluateEqualityCorrectly(
        string street1, string? apt1, string street2, string? apt2, bool expectedEqual)
    {
        // Arrange
        var address1 = new Address(street1, apt1);
        var address2 = new Address(street2, apt2);

        // Act & Assert
        address1.Equals(address2).Should().Be(expectedEqual);
        (address1 == address2).Should().Be(expectedEqual);
        (address1 != address2).Should().Be(!expectedEqual);

        if (expectedEqual)
        {
            address1.GetHashCode().Should().Be(address2.GetHashCode());
        }
    }

    // -------------------------------------------------------------------------
    // 4. Type Boundary: Different ValueObject types with identical components
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingDifferentValueObjectTypesWithSameComponents()
    {
        // Arrange: BillingCity and ShippingCity have identical string "Riyadh"
        var billing = new BillingCity("Riyadh");
        var shipping = new ShippingCity("Riyadh");

        // Act & Assert: Must return false because GetType() is different
        billing.Equals(shipping).Should().BeFalse();
        (billing == shipping).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 5. HashSet Deduplication Behavior
    // -------------------------------------------------------------------------
    [Fact]
    public void HashSet_ShouldDeduplicateEqualValueObjects()
    {
        // Arrange
        var item1 = new Money(100, "SAR");
        var item2 = new Money(100, "SAR");
        var item3 = new Money(200, "SAR");

        // Act
        var set = new HashSet<Money> { item1, item2, item3 };

        // Assert: Equal value objects merge into 1 entry
        set.Should().HaveCount(2);
        set.Should().Contain(new Money(100, "SAR"));
        set.Should().Contain(new Money(200, "SAR"));
    }

    // -------------------------------------------------------------------------
    // Test Fakes (Helpers for ValueObject Tests)
    // -------------------------------------------------------------------------
    private class Money : ValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    private class Address : ValueObject
    {
        public string Street { get; }
        public string? ApartmentNumber { get; }

        public Address(string street, string? apartmentNumber)
        {
            Street = street;
            ApartmentNumber = apartmentNumber;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Street;
            yield return ApartmentNumber;
        }
    }

    private class BillingCity : ValueObject
    {
        public string City { get; }
        public BillingCity(string city) => City = city;
        protected override IEnumerable<object?> GetEqualityComponents() { yield return City; }
    }

    private class ShippingCity : ValueObject
    {
        public string City { get; }
        public ShippingCity(string city) => City = city;
        protected override IEnumerable<object?> GetEqualityComponents() { yield return City; }
    }
}
