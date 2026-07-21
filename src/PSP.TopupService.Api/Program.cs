using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using PSP.TopupService.Application;
using PSP.TopupService.Infrastructure;
using PSP.TopupService.Infrastructure.Clients;
using PSP.TopupService.Persistence;

var builder = WebApplication.CreateBuilder(args);

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
    options.SuppressModelStateInvalidFilter = false; // built-in 400 for malformed JSON etc.
});
builder.Services.AddProblemDetails();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
