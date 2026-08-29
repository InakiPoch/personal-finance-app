namespace PersonalFinance.SharedKernel;

/// <summary>
/// Base for a domain object with its own identity and lifecycle. Two entities are equal when
/// they are the same concrete type and carry the same <typeparamref name="TId"/>.
/// </summary>
/// <typeparam name="TId">The identifier type.</typeparam>
public abstract class Entity<TId>(TId id) : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; } = id;

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) {
        return !Equals(left, right);
    }

    public bool Equals(Entity<TId>? other) {
        return other is not null
            && GetType() == other.GetType()
            && EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) {
        return obj is Entity<TId> other && Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(GetType(), Id);
    }
}
