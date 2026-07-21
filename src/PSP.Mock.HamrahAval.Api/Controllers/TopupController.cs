using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PSP.Mock.HamrahAval.Api.Options;

namespace PSP.Mock.HamrahAval.Api.Controllers;

/// <summary>
/// Hamrah-e-Aval (MCI) simulator. The endpoint shape matches what the Topup
/// service's <c>HamrahAvalClient</c> calls: POST /topup with the topup payload,
/// returning either the operator reference or a failure. Failure injection
/// (rate, delay, always-fail) lets us exercise the Topup service's Polly
/// pipeline end-to-end without a real provider.
/// </summary>
[ApiController]
[Route("")]
public sealed class TopupController : ControllerBase
{
    private readonly MockHamrahAvalOptions _options;

    public TopupController(IOptions<MockHamrahAvalOptions> options)
    {
        _options = options.Value;
    }

    [HttpPost("topup")]
    public async Task<IActionResult> Topup([FromBody] TopupRequest request, CancellationToken cancellationToken)
    {
        if (_options.MaxDelayMs > 0)
        {
            await Task.Delay(Random.Shared.Next(_options.MaxDelayMs), cancellationToken);
        }

        if (_options.AlwaysFail || ShouldFail(_options.FailureRate))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                succeeded = false,
                errorMessage = "Simulated MCI failure",
            });
        }

        var reference = $"MCI-{Guid.NewGuid():N}"[..24];
        return Ok(new { succeeded = true, reference });
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "Healthy" });

    private static bool ShouldFail(double rate)
    {
        if (rate <= 0)
        {
            return false;
        }

        if (rate >= 1)
        {
            return true;
        }

        return Random.Shared.NextDouble() < rate;
    }
}

public sealed record TopupRequest(Guid TopupId, string MobileNumber, decimal Amount, string Currency, Guid CorrelationId);
