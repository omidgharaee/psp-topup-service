using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Topups.Commands;
using PSP.TopupService.Application.Topups.DTOs;
using PSP.TopupService.Application.Topups.Queries;

namespace PSP.TopupService.Api.Controllers.V1;

/// <summary>
/// Topup endpoints. Controllers are intentionally thin: they map the request
/// to a MediatR command/query, set the correlation context, and translate the
/// Result into the appropriate HTTP response. All business logic lives in the
/// Application layer.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class TopupsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICorrelationContext _correlation;

    public TopupsController(ISender mediator, ICorrelationContext correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    /// <summary>Creates a new topup transaction in the Pending state.</summary>
    /// <response code="202">Transaction accepted; the asynchronous payment/topup flow has been kicked off.</response>
    /// <response code="400">Validation failed; the body contains per-field errors.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TopupResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTopupRequest request, CancellationToken cancellationToken)
    {
        // Hydrate the correlation context from headers so handlers, the outbox
        // and downstream logs all carry the same ids for this request.
        _correlation.Set(
            correlationId: GetOrNewCorrelationId(),
            requestId: Guid.NewGuid(),
            traceId: HttpContext.TraceIdentifier,
            actor: User?.Identity?.Name,
            remoteIp: HttpContext.Connection.RemoteIpAddress?.ToString());

        var command = new CreateTopupCommand
        {
            MobileNumber = request.MobileNumber,
            Amount = request.Amount,
            IdempotencyKey = request.IdempotencyKey,
            Actor = _correlation.Actor,
            RemoteIp = _correlation.RemoteIp,
        };

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return result.IsIdempotentReplay
                ? AcceptedAtAction(nameof(GetById), new { version = "1", id = result.Response.TransactionId }, result.Response)
                : AcceptedAtAction(nameof(GetById), new { version = "1", id = result.Response.TransactionId }, result.Response);
        }
        catch (Application.Common.Exceptions.ValidationException vex)
        {
            foreach (var (key, values) in vex.Errors)
            {
                foreach (var msg in values)
                {
                    ModelState.AddModelError(key, msg);
                }
            }

            return ValidationProblem(ModelState);
        }
    }

    /// <summary>Returns the detailed view of a single topup.</summary>
    /// <response code="200">Topup found.</response>
    /// <response code="404">Topup id does not exist.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TopupDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTopupByIdQuery { TopupId = id }, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error.Type switch
            {
                SharedKernel.Results.ErrorType.NotFound => NotFound(Problem(result.Error)),
                _ => BadRequest(Problem(result.Error)),
            };
    }

    private Guid GetOrNewCorrelationId()
    {
        if (Request.Headers.TryGetValue("X-Correlation-Id", out var header) && Guid.TryParse(header, out var g))
        {
            return g;
        }

        return Guid.NewGuid();
    }

    private ProblemDetails Problem(SharedKernel.Results.Error error) => new()
    {
        Type = $"https://errors.psp.local/{error.Code}",
        Title = error.Type.ToString(),
        Detail = error.Message,
        Status = StatusCodes.Status400BadRequest,
        Instance = HttpContext.Request.Path,
    };
}
