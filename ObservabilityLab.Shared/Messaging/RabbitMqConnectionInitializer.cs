using Microsoft.Extensions.Hosting;

namespace ObservabilityLab.Shared.Messaging;

/// <summary>
/// Eagerly opens the RabbitMQ connection at host startup so that connection failures
/// surface immediately and <see cref="RabbitMqConnectionManager.GetConnectionAsync"/>
/// is awaited on the host's startup path rather than blocked.
/// Teardown is owned by <see cref="RabbitMqConnectionManager"/> (singleton
/// <see cref="IAsyncDisposable"/>); this service must not dispose it.
/// </summary>
internal sealed class RabbitMqConnectionInitializer(RabbitMqConnectionManager manager) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
        => manager.GetConnectionAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
