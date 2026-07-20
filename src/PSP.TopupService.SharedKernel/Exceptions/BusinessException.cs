namespace PSP.TopupService.SharedKernel.Exceptions;

/// <summary>
/// Raised when a business rule is violated in a context where a <c>Result</c>
/// cannot be propagated (e.g. inside a domain-event handler). The global
/// exception middleware maps it to RFC7807 with a 422 status code.
/// </summary>
public class BusinessException : DomainException
{
    public BusinessException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public BusinessException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public override string Code { get; }
}
