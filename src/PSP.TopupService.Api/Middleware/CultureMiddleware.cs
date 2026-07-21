using System.Globalization;

namespace PSP.TopupService.Api.Middleware;

/// <summary>
/// Forces Persian culture for the request so Persian log messages and
/// localized error text render consistently regardless of the server's locale.
/// Numeric formats stay invariant for serialization (handled in DTOs).
/// </summary>
public sealed class CultureMiddleware
{
    private const string PersianCulture = "fa-IR";

    private readonly RequestDelegate _next;

    public CultureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var culture = new CultureInfo(PersianCulture);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        await _next(context);
    }
}
