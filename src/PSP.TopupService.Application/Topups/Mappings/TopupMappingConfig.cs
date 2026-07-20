using Mapster;
using PSP.TopupService.Application.Topups.Commands;
using PSP.TopupService.Application.Topups.DTOs;

namespace PSP.TopupService.Application.Topups.Mappings;

/// <summary>
/// Mapster configuration for the Topup feature. Register with
/// <c>TypeAdapterConfig.GlobalSettings.Scan(typeof(TopupMappingConfig).Assembly)</c>
/// during application startup.
/// </summary>
public sealed class TopupMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // API request -> command. The command is the authoritative input shape
        // for the application layer; the request DTO maps to it. Input
        // validation ensures MobileNumber/Amount are populated before mapping,
        // so the null-forgiving operators are safe.
        config.NewConfig<CreateTopupRequest, CreateTopupCommand>()
            .Map(d => d.MobileNumber, s => s.MobileNumber!)
            .Map(d => d.Amount, s => s.Amount)
            .Map(d => d.IdempotencyKey, s => s.IdempotencyKey)
            .Ignore(d => d.Actor)
            .Ignore(d => d.RemoteIp);
    }
}
