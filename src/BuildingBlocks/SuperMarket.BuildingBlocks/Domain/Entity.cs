namespace SuperMarket.BuildingBlocks.Domain;

/// <summary>
/// Abstract base class for all Domain Entities.
/// Encapsulates identity and enforces structural identity equality rather than reference equality.
/// </summary>
/// <typeparam name="TId">The type of the entity's unique identifier.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
   
    public TId Id { get; protected set; } = default!;

      protected Entity()
    {
    }

     protected Entity(TId id)
    {
        Id = id;
    }

   
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        if (obj is not Entity<TId> other)
            return false;

        return Equals(other);
    }

  
    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (other.GetType() != GetType())
            return false;

        // Transient entities (entities not yet persisted, having default Id) are only equal if they share the exact memory reference.
        if (IsTransient() || other.IsTransient())
            return false;

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <summary>
    /// Returns true if the entity has not yet been assigned a persistent identifier.
    /// </summary>
    public bool IsTransient()
    {
        return EqualityComparer<TId>.Default.Equals(Id, default!);
    }

    /// <summary>
    /// Generates a hash code consistent with identity equality.
    /// </summary>
    public override int GetHashCode()
    {
        if (IsTransient())
            return base.GetHashCode();

        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !(left == right);
    }
}
