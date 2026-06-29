using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ObservabilityLab.Shared.Messaging;

/// <summary>
/// A lightweight lease returned by <see cref="RabbitMqChannelPool.AcquireAsync"/>.
/// Returning the channel to the pool by <c>await using</c> the lease.
/// </summary>
public readonly struct ChannelLease(RabbitMqChannelPool pool, IChannel channel) : IAsyncDisposable
{
    public IChannel Channel { get; } = channel;

    public ValueTask DisposeAsync() => pool.ReturnAsync(Channel);
}

/// <summary>
/// Bounded pool of <see cref="IChannel"/> instances that are re-used across publishes to
/// eliminate per-message allocation and broker channel churn. All pooled channels have
/// publisher confirmations enabled (set at creation; cannot be toggled after the fact).
/// <para>
/// When all channels are leased, <see cref="AcquireAsync"/> applies backpressure by awaiting
/// a free slot rather than opening new channels unboundedly.
/// </para>
/// </summary>
public sealed class RabbitMqChannelPool : IAsyncDisposable
{
    private static readonly CreateChannelOptions ConfirmedChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    private readonly RabbitMqConnectionManager _connectionManager;
    private readonly ILogger<RabbitMqChannelPool> _logger;
    private readonly int _maxChannels;
    private readonly SemaphoreSlim _capacity;
    private readonly ConcurrentQueue<IChannel> _idle = new();
    private bool _disposed;

    public RabbitMqChannelPool(
        RabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqChannelPool> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
        _maxChannels = Math.Max(1, options.Value.MaxChannels);
        _capacity = new SemaphoreSlim(_maxChannels, _maxChannels);
    }

    /// <summary>
    /// Acquires a channel from the pool. If all <see cref="RabbitMqOptions.MaxChannels"/> slots
    /// are leased, this awaits until one is returned (backpressure).
    /// Dispose the returned <see cref="ChannelLease"/> (via <c>await using</c>) to return the
    /// channel to the pool.
    /// </summary>
    public async ValueTask<ChannelLease> AcquireAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _capacity.WaitAsync(ct);
        try
        {
            var channel = await GetOrCreateChannelAsync(ct);
            return new ChannelLease(this, channel);
        }
        catch
        {
            // Never leak a capacity slot if hand-out fails.
            _capacity.Release();
            throw;
        }
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken ct)
    {
        // Drain the idle queue, skipping stale channels.
        // Stale channels can appear after a broker disconnect + automatic recovery, where the
        // connection is re-established but previously created channels are closed.
        while (_idle.TryDequeue(out var candidate))
        {
            if (candidate.IsOpen)
                return candidate;

            _logger.LogDebug("Discarding stale pooled channel; a replacement will be created.");
            await candidate.DisposeAsync();
        }

        // No idle channels available — create a fresh one under the capacity cap.
        return await CreateChannelAsync(ct);
    }

    private async Task<IChannel> CreateChannelAsync(CancellationToken ct)
    {
        var connection = await _connectionManager.GetConnectionAsync(ct);
        return await connection.CreateChannelAsync(ConfirmedChannelOptions, ct);
    }

    /// <summary>Called by <see cref="ChannelLease.DisposeAsync"/>; returns the channel to the pool.</summary>
    internal async ValueTask ReturnAsync(IChannel channel)
    {
        if (!_disposed && channel.IsOpen)
        {
            _idle.Enqueue(channel);
        }
        else
        {
            await channel.DisposeAsync();
        }

        // Release the capacity slot so the next AcquireAsync can proceed.
        // Guard against the semaphore being disposed in the shutdown race.
        if (!_disposed)
        {
            try
            {
                _capacity.Release();
            }
            catch (ObjectDisposedException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        while (_idle.TryDequeue(out var channel))
        {
            try
            {
                await channel.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Error closing pooled channel during shutdown: {Message}", ex.Message);
            }

            await channel.DisposeAsync();
        }

        _capacity.Dispose();

        _logger.LogDebug("RabbitMQ channel pool ({Max} slot(s)) disposed.", _maxChannels);
    }
}
