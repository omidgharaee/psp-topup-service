namespace PSP.TopupService.SharedKernel.Results;

/// <summary>
/// The non-generic <see cref="Result"/> for operations that have no return value.
/// Use <see cref="Result{TValue}"/> when the operation produces a value.
/// </summary>
public readonly struct Result
{
    public Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result must carry no error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary> Chains another operation only when this result is successful.</summary>
    public Result OnSuccess(Func<Result> next) => IsSuccess ? next() : this;

    /// <summary>Projects the success value into a new result.</summary>
    public Result<TValue> Map<TValue>(Func<TValue> next) =>
        IsSuccess ? Result.Success(next()) : Result.Failure<TValue>(Error);

    /// <summary>Performs an action when successful, otherwise returns the current failure.</summary>
    public Result Ensure(Func<bool> predicate, Error error) =>
        IsFailure ? this : predicate() ? this : Failure(error);
}
