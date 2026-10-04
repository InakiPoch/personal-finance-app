namespace PersonalFinance.SharedKernel;

/// <summary>
/// A <see cref="Result"/> that also carries a <typeparamref name="TValue"/> on success.
/// </summary>
/// <typeparam name="TValue">The value produced on success.</typeparam>
public sealed class Result<TValue> : Result {
    public TValue Value => IsSuccess
        ? value
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    private readonly TValue value;

    internal Result(TValue value, bool isSuccess, Error error) : base(isSuccess, error) {
        this.value = value;
    }

    public static implicit operator Result<TValue>(TValue value) {
        return Success(value);
    }

    public static implicit operator Result<TValue>(Error error) {
        return Failure<TValue>(error);
    }
}
