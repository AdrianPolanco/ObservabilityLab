namespace ObservabilityLab.Shared.Messaging;

/// <summary>
/// Configuration options for the RabbitMQ connection, bound from the "RabbitMq"
/// section in appsettings.json via IOptions / IOptionsMonitor.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";

    /// <summary>
    /// Maximum number of pooled <see cref="RabbitMQ.Client.IChannel"/> instances held by
    /// <see cref="RabbitMqChannelPool"/>. Defaults to 10; must be at least 1.
    /// Overridable per service via the "RabbitMq" section in appsettings.json.
    /// </summary>
    public int MaxChannels { get; init; } = 10;

    /// <summary>
    /// Number of messages the broker delivers to a consumer before requiring an ACK
    /// (BasicQos prefetch count). Defaults to 10. Applied per consumer channel.
    /// Overridable per service via the "RabbitMq" section in appsettings.json.
    /// </summary>
    public ushort PrefetchCount { get; init; } = 10;
}
