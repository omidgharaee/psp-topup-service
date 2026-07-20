using System.Diagnostics.CodeAnalysis;

namespace PSP.TopupService.SharedKernel.Results;

/// <summary>
/// A strongly-typed error record used throughout the application layer. Errors
/// are immutable value types so they can be safely compared, cached and logged.
/// </summary>
public sealed record Error
{
    /// <summary> Sentinel used to indicate "no error" — never returned from a failed Result.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    private Error(string code, string message, ErrorType type)
    {
        Code = code;
        Message = message;
        Type = type;
    }

    /// <summary>Stable, machine-readable error code (e.g. "Topup.InvalidAmount").</summary>
    public string Code { get; }

    /// <summary>Human-readable description, safe to surface to API consumers.</summary>
    public string Message { get; }

    /// <summary>Broad category of the error for mapping to HTTP / messaging responses.</summary>
    public ErrorType Type { get; }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    /// <summary>True when this instance represents the absence of an error.</summary>
    [MemberNotNullWhen(false, nameof(Code))]
    [MemberNotNullWhen(false, nameof(Message))]
    public bool IsNone => string.IsNullOrEmpty(Code);

    public override string ToString() => IsNone ? "None" : $"[{Type}] {Code}: {Message}";
}
