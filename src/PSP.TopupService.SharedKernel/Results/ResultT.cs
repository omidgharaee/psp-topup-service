namespace PSP.TopupService.SharedKernel.Results;

/// <summary>
/// The generic Result that carries a payload on success and an <see cref="Error"/> on failure.
/// Business-rule violations should always be modelled with Result and never by throwing.
/// </summary>
/// <typeparam name="TValue">The type of the success payload.</typeparam>
public readonly struct Result<TValue>
{
    public Result(TValue? value, bool isSuccess, Error error)
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
        Value = value;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public TValue? Value { get; }

    public static implicit operator Result<TValue>(TValue value) => new(value, true, Error.None);
    public static implicit operator Result<TValue>(Error error) => new(default, false, error);

    /// <summary>Throws <see cref="InvalidOperationException"/> if accessed on a failed result.</summary>
    public TValue GetSuccessValue() =>
        IsSuccess
            ? Value!
            : throw new InvalidOperationException($"Cannot access value of a failed result: {Error}.");

    /// <summary> Returns the success value or the supplied fallback when failed.</summary>
    public TValue Or(TValue fallback) => IsSuccess ? Value! : fallback;

    /// <summary>Returns the success value or throws the mapped exception on failure.</summary>
    public TValue OrThrow(Func<Error, Exception> exceptionFactory) =>
        IsSuccess ? Value! : throw exceptionFactory(Error);

    public Result<TNext> Map<TNext>(Func<TValue, TNext> mapper) =>
        IsSuccess ? Result.Success(mapper(Value!)) : Result.Failure<TNext>(Error);

    public Result<TNext> Bind<TNext>(Func<TValue, Result<TNext>> next) =>
        IsSuccess ? next(Value!) : Result.Failure<TNext>(Error);

    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error) =>
        IsFailure ? this : predicate(Value!) ? this : Result.Failure<TValue>(error);

    public TValue Match(Func<TValue, TValue> onSuccess, Func<Error, TValue> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error);
}
