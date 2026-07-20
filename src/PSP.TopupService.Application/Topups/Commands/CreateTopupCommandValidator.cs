using FluentValidation;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Topups.Commands;

/// <summary>
/// FluentValidation validator for <see cref="CreateTopupCommand"/>. Performs
/// input-shape validation only — the domain's value objects perform semantic
/// validation at construction. Failing validation surfaces as a 400 with the
/// detailed error list via the ValidationBehavior pipeline.
/// </summary>
public sealed class CreateTopupCommandValidator : AbstractValidator<CreateTopupCommand>
{
    public CreateTopupCommandValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("MobileNumber is required.")
            .Must(BePotentiallyValidMobile).WithMessage("MobileNumber must be an 11-digit Iranian number starting with 09.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .GreaterThanOrEqualTo(Money.MinimumTopupAmount)
                .WithMessage($"Amount must be at least {Money.MinimumTopupAmount} IRR.")
            .LessThanOrEqualTo(Money.MaximumTopupAmount)
                .WithMessage($"Amount must not exceed {Money.MaximumTopupAmount} IRR.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(256).WithMessage("IdempotencyKey cannot exceed 256 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));

        RuleFor(x => x.Actor)
            .MaximumLength(128).WithMessage("Actor cannot exceed 128 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Actor));

        RuleFor(x => x.RemoteIp)
            .MaximumLength(64).WithMessage("RemoteIp cannot exceed 64 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.RemoteIp));
    }

    /// <summary>
    /// Cheap pre-check: only verifies the canonical shape, not the full
    /// normalisation. The authoritative check happens inside MobileNumber.Create.
    /// </summary>
    private static bool BePotentiallyValidMobile(string raw)
    {
        // Acceptable inputs include whitespace/separators and the +98 prefix;
        // we just want to reject obviously broken strings early.
        var normalized = raw.Replace(" ", string.Empty).Replace("-", string.Empty);
        return MobileNumber.TryCreate(normalized) is not null;
    }
}
