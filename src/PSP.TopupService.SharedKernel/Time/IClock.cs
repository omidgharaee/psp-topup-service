namespace PSP.TopupService.SharedKernel.Time;

/// <summary>
/// Abstraction over the system clock so that time-dependent domain logic
/// (expiry windows, retry schedules, audit timestamps) is deterministic under
/// test. The production implementation returns <see cref="DateTime.UtcNow"/>;
/// tests inject a fixed clock.
/// </summary>
public interface IClock
{
    /// <summary>The current UTC instant.</summary>
    DateTime UtcNow { get; }

    /// <summary>Convenience offset for retry / delay calculations.</summary>
    DateTimeOffset UtcNowOffset => new(UtcNow, TimeSpan.Zero);
}

/// <summary>Default production clock backed by <see cref="DateTime.UtcNow"/>.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Deterministic clock used in tests. The instant can be advanced explicitly.
/// </summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTime initialUtc)
    {
        if (initialUtc.Kind != DateTimeKind.Utc)
        {
            initialUtc = DateTime.SpecifyKind(initialUtc, DateTimeKind.Utc);
        }

        UtcNow = initialUtc;
    }

    public DateTime UtcNow { get; private set; }

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);

    public void Set(DateTime utcInstant)
    {
        UtcNow = utcInstant.Kind == DateTimeKind.Utc
            ? utcInstant
            : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);
    }
}
