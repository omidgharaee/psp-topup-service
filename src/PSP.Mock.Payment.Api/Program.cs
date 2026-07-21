using MassTransit;
using PSP.Mock.Payment.Api.Consumers;
using PSP.Mock.Payment.Api.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MockPaymentOptions>(builder.Configuration.GetSection(MockPaymentOptions.SectionName));

builder.Services.AddMassTransit(cfg =>
{
    cfg.SetKebabCaseEndpointNameFormatter();

    cfg.AddConsumer<PaymentRequestedConsumer>();
    cfg.AddConsumer<AdviceRequestedConsumer>();
    cfg.AddConsumer<ReverseRequestedConsumer>();

    cfg.UsingRabbitMq((context, bus) =>
    {
        var section = context.GetRequiredService<IConfiguration>().GetSection("RabbitMq");
        var host = section["Host"] ?? "localhost";
        var port = int.Parse(section["Port"] ?? "5672", System.Globalization.CultureInfo.InvariantCulture);
        var user = section["Username"] ?? "guest";
        var pass = section["Password"] ?? "guest";
        var vhost = string.IsNullOrEmpty(section["VirtualHost"]) || section["VirtualHost"] == "/"
            ? string.Empty
            : $"/{Uri.EscapeDataString(section["VirtualHost"]!)}";
        var uri = new Uri($"amqp://{host}:{port}{vhost}");

        bus.Host(uri, h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        bus.UseMessageRetry(r => r.Intervals(100, 500, 1000));
        bus.ConfigureEndpoints(context);
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
