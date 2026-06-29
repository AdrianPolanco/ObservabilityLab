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
}
