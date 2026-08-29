namespace PersonalFinance.SharedKernel;

/// <summary>
/// Base for an immutable domain concept with no identity of its own: two value objects are
/// equal when they are the same concrete type and every component from <see cref="GetEqualityComponents"/> is equal, in order.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject> {
    public static bool operator ==(ValueObject? left, ValueObject? right) {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right) {
        return !Equals(left, right);
    }

    public bool Equals(ValueObject? other) {
        return other is not null
            && GetType() == other.GetType()
            && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) {
        return obj is ValueObject other && Equals(other);
    }

    public override int GetHashCode() {
        var hash = new HashCode();
        hash.Add(GetType());
        foreach(var component in GetEqualityComponents()) {
            hash.Add(component);
        }
        return hash.ToHashCode();
    }

    protected abstract IEnumerable<object?> GetEqualityComponents();
}
