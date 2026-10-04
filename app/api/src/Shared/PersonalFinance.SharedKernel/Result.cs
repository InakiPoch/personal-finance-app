namespace PersonalFinance.SharedKernel;

/// <summary>
/// The outcome of an operation that can fail without throwing: either success, or a
/// failure carrying a non-empty <see cref="Error"/>.
/// </summary>
public class Result {
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    
    public Error Error { get; }

    protected Result(bool isSuccess, Error error) {
        switch(isSuccess) {
            case true when error != Error.None:
                throw new InvalidOperationException("A successful result cannot carry an error.");
            case false when error == Error.None:
                throw new InvalidOperationException("A failed result must carry an error.");
            default:
                IsSuccess = isSuccess;
                Error = error;
            break;
        }
    }

    public static Result Success() {
        return new Result(true, Error.None);
    }

    public static Result Failure(Error error) {
        return new Result(false, error);
    }

    public static Result<TValue> Success<TValue>(TValue value) {
        return new Result<TValue>(value, true, Error.None);
    }

    public static Result<TValue> Failure<TValue>(Error error) {
        return new Result<TValue>(default!, false, error);
    }
}
