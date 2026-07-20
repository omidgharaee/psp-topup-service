namespace PSP.TopupService.SharedKernel.Guards;

/// <summary>
/// Reusable pre-condition checks for constructors and factory methods. Throws
/// <see cref="ArgumentException"/> / <see cref="ArgumentNullException"/> on
/// violation — these are programmer errors, not business errors.
/// </summary>
public static class Guard
{
    public static T NotNull<T>(T? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    public static string NotNullOrWhiteSpace(string? value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    public static string NotEmpty(string value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }

    public static Guid NotEmpty(Guid value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Guid value cannot be empty.", paramName);
        }

        return value;
    }

    public static T GreaterThan<T>(T value, T min, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value is null || value.CompareTo(min) <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be greater than {min}.");
        }

        return value;
    }

    public static T GreaterThanOrEqual<T>(T value, T min, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value is null || value.CompareTo(min) < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be greater than or equal to {min}.");
        }

        return value;
    }

    public static T InRange<T>(T value, T min, T max, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        if (value is null || value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be in the range [{min}, {max}].");
        }

        return value;
    }

    public static void Against(Func<bool> predicate, string message, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(predicate))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (predicate())
        {
            throw new ArgumentException(message, paramName);
        }
    }
}
