using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PSP.TopupService.Application;
using PSP.TopupService.Infrastructure;
using PSP.TopupService.Persistence;
using PSP.TopupService.Worker.Options;
using PSP.TopupService.Worker.Outbox;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<OutboxPublisherOptions>(builder.Configuration.GetSection(OutboxPublisherOptions.SectionName));
builder.Services.AddHostedService<OutboxPublisherWorker>();

var host = builder.Build();

await host.RunAsync();
