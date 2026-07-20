using MediatR;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Topups.DTOs;
using PSP.TopupService.SharedKernel.Results;

namespace PSP.TopupService.Application.Topups.Queries;

/// <summary>Query that returns the detailed view of a single topup by id.</summary>
public sealed record GetTopupByIdQuery : IRequest<Result<TopupDetailsDto>>
{
    public required Guid TopupId { get; init; }
}

public sealed class GetTopupByIdQueryHandler : IRequestHandler<GetTopupByIdQuery, Result<TopupDetailsDto>>
{
    private readonly ITopupRepository _repository;

    public GetTopupByIdQueryHandler(ITopupRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<TopupDetailsDto>> Handle(GetTopupByIdQuery request, CancellationToken cancellationToken)
    {
        if (request.TopupId == Guid.Empty)
        {
            return Result.Failure<TopupDetailsDto>(Error.Validation(
                "Topup.InvalidId",
                "TopupId cannot be empty."));
        }

        var topup = await _repository.GetByIdAsync(request.TopupId, cancellationToken);
        if (topup is null)
        {
            return Result.Failure<TopupDetailsDto>(Error.NotFound(
                "Topup.NotFound",
                $"Topup transaction '{request.TopupId}' was not found."));
        }

        var dto = new TopupDetailsDto
        {
            TransactionId = topup.Id,
            Status = topup.Status.ToString(),
            MobileNumber = topup.MobileNumber.Value,
            Amount = topup.Amount.Value,
            Currency = topup.Amount.Currency,
            CorrelationId = topup.CorrelationId,
            BankReference = topup.BankReference?.ToString(),
            MciReference = topup.MciReference?.ToString(),
            FailureReason = topup.FailureReason == Domain.Topups.Enums.TopupFailureReason.None
                ? null
                : topup.FailureReason,
            FailureMessage = topup.FailureMessage,
            CreatedOnUtc = topup.CreatedOnUtc,
            CompletedAtUtc = topup.CompletedAtUtc,
            AttemptCount = topup.AttemptCount,
        };

        return Result.Success(dto);
    }
}
