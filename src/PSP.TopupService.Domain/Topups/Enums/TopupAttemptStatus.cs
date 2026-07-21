namespace PSP.TopupService.Domain.Topups.Enums;

/// <summary>Status of a single <see cref="Entities.TopupAttempt"/>.</summary>
public enum TopupAttemptStatus
{
    /// <summary>The provider call is in flight.</summary>
    InProgress = 1,

    /// <summary>The provider returned a definitive success.</summary>
    Succeeded = 2,

    /// <summary>The provider call failed (timeout, 5xx, business rejection).</summary>
    Failed = 3,
}
