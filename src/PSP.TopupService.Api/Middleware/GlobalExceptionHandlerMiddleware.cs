using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Exceptions;
using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.Api.Middleware;

/// <summary>
/// Catches every unhandled exception that escaped the MediatR pipeline and
/// converts it to an RFC 7807 ProblemDetails response. Domain and business
/// exceptions are mapped to the right status code; everything else becomes a
/// 500 with a generic message (the details are logged server-side, not leaked).
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly ProblemDetailsFactory _problemFactory;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        ProblemDetailsFactory problemFactory)
    {
        _next = next;
        _logger = logger;
        _problemFactory = problemFactory;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlation)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex, correlation);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex, ICorrelationContext correlation)
    {
        var (status, title, detail, code) = Map(ex);

        _logger.LogError(ex, "استثنای غیرمنتظره - کد: {Code} - شناسه همبستگی: {CorrelationId}", code, correlation.CorrelationId);

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problem = _problemFactory.CreateProblemDetails(
            context,
            statusCode: (int)status,
            title: title,
            type: $"https://errors.psp.local/{code}",
            detail: detail,
            instance: context.Request.Path);

        // Annotate with correlation id so clients can quote it in support tickets.
        problem.Extensions["correlationId"] = correlation.CorrelationId;

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (HttpStatusCode Status, string Title, string Detail, string Code) Map(Exception ex) => ex switch
    {
        ValidationException ve => (HttpStatusCode.BadRequest, "Validation failed", ve.Message, ValidationException.Code),
        NotFoundException nf => (HttpStatusCode.NotFound, "Resource not found", nf.Message, nf.Code),
        BusinessException be => (HttpStatusCode.UnprocessableEntity, "Business rule violation", be.Message, be.Code),
        ConcurrencyException => (HttpStatusCode.Conflict, "Concurrency conflict", ex.Message, "Concurrency.Conflict"),
        InfrastructureException ie => (HttpStatusCode.ServiceUnavailable, "Upstream unavailable", ie.Message, ie.Code),
        DomainException de => (HttpStatusCode.UnprocessableEntity, "Domain error", de.Message, de.Code),
        _ => (HttpStatusCode.InternalServerError, "Internal server error", "خطای داخلی سرور رخ داده است", "Internal.Error"),
    };
}
