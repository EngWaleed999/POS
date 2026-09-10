namespace SuperMarket.BuildingBlocks.Domain;

/// <summary>
/// Abstract base class for Value Objects in Domain-Driven Design.
/// Enforces immutability and structural/value equality instead of reference or identity equality.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Yields each atomic property of the value object that participates in equality checking.
    /// </summary>
    /// <returns>An enumerable of atomic values.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj is not ValueObject other)
            return false;

        return Equals(other);
    }

    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(default(int), (current, obj) => HashCode.Combine(current, obj?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }
}
