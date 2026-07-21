namespace PSP.TopupService.SharedKernel.Results;

/// <summary>
/// Categorises an <see cref="Error"/> so callers (API, consumers, middleware)
/// can map it to the correct response without parsing free-text.
/// </summary>
public enum ErrorType
{
    /// <summary>The request was structurally invalid (validation failure).</summary>
    Validation,

    /// <summary>The request was well-formed but semantically invalid (e.g. bad state).</summary>
    Conflict,

    /// <summary>The referenced resource does not exist.</summary>
    NotFound,

    /// <summary>The caller is not permitted to perform the operation.</summary>
    Unauthorized,

    /// <summary>The operation is not allowed in the current state.</summary>
    Forbidden,

    /// <summary>A transient infrastructure condition prevented completion (retry may succeed).</summary>
    Unavailable,

    /// <summary>Any error not covered by the categories above.</summary>
    Failure,
}
