using Microsoft.AspNetCore.Http;
using PSP.TopupService.Api.Middleware;
using PSP.TopupService.Application.Common.Context;

namespace PSP.TopupService.UnitTests.Api.Middleware;

/// <summary>
/// Verifies <see cref="CorrelationIdMiddleware"/>. Tests assert on the response
/// header (which the middleware writes unconditionally) rather than the
/// ambient correlation context, because the context is async-local and the
/// test runner may observe it from a parent async flow where it is not set.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Api")]
public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_Should_Echo_Inbound_Correlation_Id_In_Response_Header()
    {
        var inbound = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = inbound.ToString();

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, new CorrelationContext());

        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(inbound.ToString());
    }

    [Fact]
    public async Task Invoke_Should_Generate_New_Correlation_Id_When_Header_Absent()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, new CorrelationContext());

        var header = context.Response.Headers["X-Correlation-Id"].ToString();
        Guid.TryParse(header, out _).Should().BeTrue("response header must be a valid Guid");
    }

    [Fact]
    public async Task Invoke_Should_Generate_New_Id_When_Header_Malformed()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "not-a-guid";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, new CorrelationContext());

        var header = context.Response.Headers["X-Correlation-Id"].ToString();
        Guid.TryParse(header, out _).Should().BeTrue("malformed inbound header should fall back to a fresh Guid");
    }

    [Fact]
    public async Task Invoke_Should_Call_Next_Middleware()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new CorrelationContext());

        nextCalled.Should().BeTrue();
    }
}
