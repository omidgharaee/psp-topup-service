namespace PSP.TopupService.Infrastructure.Messaging.Configuration;

/// <summary>Connection and topology options for the RabbitMQ broker.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    /// <summary>Top-level exchange for topup-related integration events.</summary>
    public string TopupEventsExchange { get; set; } = "topup.events";

    /// <summary>Exchange used by the Bank mock for payment-completion events.</summary>
    public string PaymentEventsExchange { get; set; } = "payment.events";

    /// <summary>Queue name prefix; instances append a consumer-specific suffix.</summary>
    public string QueuePrefix { get; set; } = "psp.topup";
}
