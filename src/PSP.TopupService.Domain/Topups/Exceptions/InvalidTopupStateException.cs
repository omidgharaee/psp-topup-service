using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.Domain.Topups.Exceptions;

/// <summary>
/// Raised when an operation is attempted on a topup transaction that is not in
/// a state which permits it (e.g. completing a payment on an already-completed
/// topup). Indicates a programmer/protocol error rather than user input.
/// </summary>
public sealed class InvalidTopupStateException : DomainException
{
    public InvalidTopupStateException(Guid topupId, TopupStatus current, string attemptedOperation)
        : base($"Cannot '{attemptedOperation}' on topup {topupId} because it is in the '{current}' state.")
    {
        TopupId = topupId;
        CurrentStatus = current;
        AttemptedOperation = attemptedOperation;
    }

    public override string Code => "Topup.InvalidState";

    public Guid TopupId { get; }

    public TopupStatus CurrentStatus { get; }

    public string AttemptedOperation { get; }
}
