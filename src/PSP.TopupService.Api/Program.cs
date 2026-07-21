using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using PSP.TopupService.Api.Extensions;
using PSP.TopupService.Api.Middleware;
using PSP.TopupService.Application;
using PSP.TopupService.Infrastructure;
using PSP.TopupService.Infrastructure.Clients;
using PSP.TopupService.Persistence;
using Serilog;

// ----- Serilog bootstrap -----
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false).Build())
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Async(a => a.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code, formatProvider: System.Globalization.CultureInfo.InvariantCulture))
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ----- Layers -----
    builder.Services.AddApplication();
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddBankClient(builder.Configuration);
    builder.Services.AddHamrahAvalClient(builder.Configuration);

    // ----- API versioning -----
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // ----- MVC + ProblemDetails -----
    builder.Services.AddControllers();
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
    });
    builder.Services.AddProblemDetails();

    // ----- Health -----
    builder.Services.AddTopupHealthChecks();

    // ----- Swagger -----
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "PSP Topup Service",
            Version = "v1",
            Description = "Mobile topup transactional service (Clean Architecture / DDD).",
        });
    });

    var app = builder.Build();

    // ----- Middleware pipeline -----
    app.UseMiddleware<CultureMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapTopupHealthEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "راه‌اندازی میزبان با شکست مواجه شد");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
