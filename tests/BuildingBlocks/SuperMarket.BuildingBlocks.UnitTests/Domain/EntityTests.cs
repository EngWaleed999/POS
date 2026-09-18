using FluentAssertions;
using SuperMarket.BuildingBlocks.Domain;

namespace SuperMarket.BuildingBlocks.UnitTests.Domain;

public class EntityTests
{
    // -------------------------------------------------------------------------
    // 1. Same Memory Reference (ENT-01)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingSameMemoryReference()
    {
        // Arrange
        var entity = new OrderTestEntity(Guid.NewGuid());
        var sameReference = entity;

        // Act
        var result = entity.Equals(sameReference);
        var operatorResult = entity == sameReference;

        // Assert
        result.Should().BeTrue();
        operatorResult.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // 2. Same Id & Same Concrete Type (ENT-02)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnTrue_WhenEntitiesHaveSameIdAndSameType()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new OrderTestEntity(id) { OrderNumber = "ORD-001" };
        var entity2 = new OrderTestEntity(id) { OrderNumber = "ORD-999" };

        // Act & Assert
        entity1.Equals(entity2).Should().BeTrue();
        (entity1 == entity2).Should().BeTrue();
        (entity1 != entity2).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 3. Different Ids (ENT-03)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenEntitiesHaveDifferentIds()
    {
        // Arrange
        var entity1 = new OrderTestEntity(Guid.NewGuid());
        var entity2 = new OrderTestEntity(Guid.NewGuid());

        // Act & Assert
        entity1.Equals(entity2).Should().BeFalse();
        (entity1 == entity2).Should().BeFalse();
        (entity1 != entity2).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // 4. Null Comparison Safety (ENT-04)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparedWithNull()
    {
        // Arrange
        var entity = new OrderTestEntity(Guid.NewGuid());

        // Act & Assert
        entity.Equals(null).Should().BeFalse();
        (entity == null).Should().BeFalse();
        (null == entity).Should().BeFalse();
        (entity != null).Should().BeTrue();
        (null != entity).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // 5. Both Operands Null (ENT-05)
    // -------------------------------------------------------------------------
    [Fact]
    public void EqualityOperator_ShouldReturnTrue_WhenBothOperandsAreNull()
    {
        // Arrange
        OrderTestEntity? left = null;
        OrderTestEntity? right = null;

        // Act & Assert
        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 6. Cross-Entity Type Trap (ENT-06)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingDifferentEntityTypesWithSameId()
    {
        // Arrange: An Order and a Customer share the exact same Guid ID
        var sharedId = Guid.NewGuid();
        var order = new OrderTestEntity(sharedId);
        var customer = new CustomerTestEntity(sharedId);

        // Act & Assert
        order.Equals(customer).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 7. Inheritance / Subclassing Trap (ENT-07)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingDerivedTypeWithBaseTypeHavingSameId()
    {
        // Arrange
        var sharedId = Guid.NewGuid();
        var baseOrder = new OrderTestEntity(sharedId);
        var specialOrder = new SpecialOrderTestEntity(sharedId);

        // Act & Assert
        baseOrder.Equals(specialOrder).Should().BeFalse();
        (baseOrder == specialOrder).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 8. Transient Entities Trap (ENT-08)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenBothEntitiesAreTransientEvenWithDefaultId()
    {
        // Arrange: Two brand new unsaved orders with Guid.Empty (Default Id)
        var transientOrder1 = new OrderTestEntity();
        var transientOrder2 = new OrderTestEntity();

        // Act & Assert: Must NOT be equal because neither has persistent identity
        transientOrder1.Equals(transientOrder2).Should().BeFalse();
        (transientOrder1 == transientOrder2).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 9. IsTransient Verification (ENT-09)
    // -------------------------------------------------------------------------
    [Fact]
    public void IsTransient_ShouldCorrectlyIdentifyPersistedVsUnsavedEntities()
    {
        // Arrange
        var transientEntity = new OrderTestEntity();
        var persistedEntity = new OrderTestEntity(Guid.NewGuid());

        // Act & Assert
        transientEntity.IsTransient().Should().BeTrue();
        persistedEntity.IsTransient().Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 10. HashCode Consistency Contract (ENT-10)
    // -------------------------------------------------------------------------
    [Fact]
    public void GetHashCode_ShouldBeIdentical_WhenEntitiesAreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new OrderTestEntity(id) { OrderNumber = "ORD-001" };
        var entity2 = new OrderTestEntity(id) { OrderNumber = "ORD-002" };

        // Act & Assert: If Equals is true, GetHashCode MUST be identical
        entity1.Equals(entity2).Should().BeTrue();
        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    // -------------------------------------------------------------------------
    // 11. Transient HashCode Collision Avoidance (ENT-11)
    // -------------------------------------------------------------------------
    [Fact]
    public void GetHashCode_ShouldNotProduceIdenticalHashes_ForDifferentTransientEntities()
    {
        // Arrange: Multiple transient entities with Guid.Empty
        var transient1 = new OrderTestEntity();
        var transient2 = new OrderTestEntity();

        // Act & Assert: Transient entities rely on reference identity hash code
        transient1.GetHashCode().Should().NotBe(transient2.GetHashCode());
    }

    // -------------------------------------------------------------------------
    // 12. Transient vs Persisted Comparison (ENT-12)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparingTransientWithPersistedEntity()
    {
        // Arrange
        var transient = new OrderTestEntity();
        var persisted = new OrderTestEntity(Guid.NewGuid());

        // Act & Assert (both directions)
        transient.Equals(persisted).Should().BeFalse();
        persisted.Equals(transient).Should().BeFalse();
        (transient == persisted).Should().BeFalse();
        (persisted == transient).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 13. Transient Self-Comparison (ENT-13)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnTrue_WhenTransientEntityIsComparedToItself()
    {
        // Arrange
        var transient = new OrderTestEntity();
        var sameReference = transient;

        // Act & Assert: ReferenceEquals short-circuits and returns true even for unsaved objects
        transient.Equals(sameReference).Should().BeTrue();
        (transient == sameReference).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // 14. Completely Unrelated Non-Entity Object (ENT-14)
    // -------------------------------------------------------------------------
    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparedWithCompletelyUnrelatedObject()
    {
        // Arrange
        var entity = new OrderTestEntity(Guid.NewGuid());
        object nonEntityString = "I am just a string";
        object nonEntityNumber = 42;

        // Act & Assert: Safe type guard without InvalidCastException
        entity.Equals(nonEntityString).Should().BeFalse();
        entity.Equals(nonEntityNumber).Should().BeFalse();
    }

    [Fact]
    public void EqualsObjectOverload_ShouldHandlePolymorphicCastsAndNullCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new OrderTestEntity(id);
        object sameRef = entity;
        object equalEntity = new OrderTestEntity(id);
        object differentEntity = new OrderTestEntity(Guid.NewGuid());
        object? nullObj = null;

        // Act & Assert (invokes public override bool Equals(object? obj))
        entity.Equals(sameRef).Should().BeTrue();
        entity.Equals(equalEntity).Should().BeTrue();
        entity.Equals(differentEntity).Should().BeFalse();
        entity.Equals(nullObj).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // 15. Real Collection & Hash Behavior in HashSet (ENT-15)
    // -------------------------------------------------------------------------
    [Fact]
    public void HashSet_ShouldDeduplicatePersistedEntities_AndRetainDistinctTransientEntities()
    {
        // Arrange
        var sharedId = Guid.NewGuid();
        var persisted1 = new OrderTestEntity(sharedId) { OrderNumber = "ORD-001" };
        var persisted2 = new OrderTestEntity(sharedId) { OrderNumber = "ORD-002" }; // Same ID
        var transient1 = new OrderTestEntity();
        var transient2 = new OrderTestEntity();

        // Act
        var set = new HashSet<OrderTestEntity> { persisted1, persisted2, transient1, transient2 };

        // Assert:
        // - persisted1 and persisted2 merge into 1 entry (deduplicated by Id)
        // - transient1 and transient2 both remain distinct (2 separate entries)
        // Total count must be exactly 3!
        set.Should().HaveCount(3);
        set.Should().Contain(persisted1);
        set.Should().Contain(transient1);
        set.Should().Contain(transient2);
    }

    // -------------------------------------------------------------------------
    // 16. Parameterized IsTransient Tests across Primitive ID Types ([Theory])
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(42, false)]
    [InlineData(-1, false)]
    public void IsTransient_ShouldEvaluateCorrectly_ForIntegerIdentities(int id, bool expectedTransient)
    {
        // Arrange
        var entity = new IntIdTestEntity(id);

        // Act & Assert
        entity.IsTransient().Should().Be(expectedTransient);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", true)]
    [InlineData("a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d", false)]
    public void IsTransient_ShouldEvaluateCorrectly_ForGuidIdentities(string guidString, bool expectedTransient)
    {
        // Arrange
        var id = Guid.Parse(guidString);
        var entity = new OrderTestEntity(id);

        // Act & Assert
        entity.IsTransient().Should().Be(expectedTransient);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("ORD-100", false)]
    [InlineData("", false)] // In C#, default(string) is null, so empty string is NOT transient
    public void IsTransient_ShouldEvaluateCorrectly_ForStringIdentities(string? id, bool expectedTransient)
    {
        // Arrange
        var entity = new StringIdTestEntity(id!);

        // Act & Assert
        entity.IsTransient().Should().Be(expectedTransient);
    }

    // -------------------------------------------------------------------------
    // Test Fakes (Helper Types for Testing Abstract Base Class)
    // -------------------------------------------------------------------------
    private class OrderTestEntity : Entity<Guid>
    {
        public string OrderNumber { get; set; } = string.Empty;

        public OrderTestEntity()
        {
        }

        public OrderTestEntity(Guid id) : base(id)
        {
        }
    }

    private class CustomerTestEntity : Entity<Guid>
    {
        public CustomerTestEntity(Guid id) : base(id)
        {
        }
    }

    private class SpecialOrderTestEntity : OrderTestEntity
    {
        public SpecialOrderTestEntity(Guid id) : base(id)
        {
        }
    }

    private class IntIdTestEntity : Entity<int>
    {
        public IntIdTestEntity(int id) : base(id)
        {
        }
    }

    private class StringIdTestEntity : Entity<string>
    {
        public StringIdTestEntity(string id) : base(id)
        {
        }
    }
}
