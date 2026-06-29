using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ObservabilityLab.Shared.Messaging;

/// <summary>
/// Manages a single long-lived <see cref="IConnection"/> per process.
/// The connection is opened lazily on the first call to <see cref="GetConnectionAsync"/>
/// (with a built-in retry loop) and closed gracefully when the host shuts down via
/// <see cref="DisposeAsync"/>.
/// </summary>
public sealed class RabbitMqConnectionManager(
    IConnectionFactory connectionFactory,
    ILogger<RabbitMqConnectionManager> logger) : IAsyncDisposable
{
    private const int MaxAttempts = 5;
    private const int RetryDelaySeconds = 3;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Returns the open <see cref="IConnection"/>, creating it (with retry) if it does not
    /// yet exist or has been closed. Thread-safe via double-checked locking.
    /// </summary>
    public async Task<IConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path — lock-free, covers the >99 % case after startup.
        if (_connection is { IsOpen: true })
            return _connection;

        await _gate.WaitAsync(ct);
        try
        {
            // Re-check inside the lock in case another caller just initialised it.
            if (_connection is { IsOpen: true })
                return _connection;

            _connection = await CreateWithRetryAsync(ct);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IConnection> CreateWithRetryAsync(CancellationToken ct)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var connection = await connectionFactory.CreateConnectionAsync(ct);

                logger.LogInformation(
                    "Connected to RabbitMQ (attempt {Attempt}/{Max}).",
                    attempt, MaxAttempts);

                return connection;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                lastException = ex;

                logger.LogWarning(
                    "RabbitMQ not ready (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s...",
                    attempt, MaxAttempts, ex.Message, RetryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(RetryDelaySeconds), ct);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        logger.LogError(
            "Could not connect to RabbitMQ after {Max} attempts. Connection was NOT established.",
            MaxAttempts);

        throw new InvalidOperationException(
            $"Failed to connect to RabbitMQ after {MaxAttempts} attempts.", lastException);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Error closing RabbitMQ connection during shutdown: {Message}", ex.Message);
            }

            await _connection.DisposeAsync();
            _connection = null;
        }

        _gate.Dispose();
    }
}
