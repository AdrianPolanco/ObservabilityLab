using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObservabilityLab.Shared.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text.Json;

namespace ObservabilityLab.Shared.Messaging;

/// <summary>
/// Carries the queue name for a specific <typeparamref name="TMessage"/> consumer.
/// Registered as a singleton by <see cref="Extensions.MessagingSharedExtensions.AddRabbitMqConsumer{TMessage,THandler}"/>.
/// </summary>
public sealed class ConsumerOptions<TMessage>
{
    public required string QueueName { get; init; }
}

/// <summary>
/// Generic <see cref="BackgroundService"/> that subscribes to a single RabbitMQ queue and
/// dispatches each delivery to an <see cref="IMessageHandler{TMessage}"/> resolved from a
/// per-message DI scope (required because handlers may depend on scoped services such as
/// <c>ApplicationDbContext</c>).
/// <para>
/// Settlement policy:
/// <list type="bullet">
///   <item>Handler success (<see cref="Results.Result{T}.IsSuccess"/>) → ACK.</item>
///   <item>Handler failure or unhandled exception → NACK with <c>requeue: true</c>.</item>
///   <item>Malformed / undeserializable messages → NACK with <c>requeue: false</c> to prevent
///   hot-loops on poison messages that can never succeed.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RabbitMqConsumer<TMessage>(
    RabbitMqConnectionManager connectionManager,
    IServiceScopeFactory scopeFactory,
    ConsumerOptions<TMessage> consumerOptions,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConsumer<TMessage>> logger,
    ActivitySource activitySource) : BackgroundService
    where TMessage : class
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = consumerOptions.QueueName;

        var connection = await connectionManager.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await using (channel)
        {
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: options.Value.PrefetchCount,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) => OnMessageAsync(channel, ea, stoppingToken);

            await channel.BasicConsumeAsync(
                queue: queue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            logger.LogInformation(
                "RabbitMQ consumer started on queue {Queue} (prefetch {Prefetch}).",
                queue, options.Value.PrefetchCount);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — host cancelled stoppingToken.
                logger.LogInformation("Shutting down consumer...");
            }
            finally
            {
                logger.LogInformation("RabbitMQ consumer stopping on queue {Queue}.", queue);
            }
        }
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        // Pull the trace context the publisher stamped into the message headers (see
        // MessagingTraceContext / RabbitMqPublisher). We're on RabbitMQ's own delivery callback here —
        // Activity.Current has no relationship to the request that originally published this message —
        // so this extracted context is the *only* way to reconnect this span to that trace.
        var parentContext = MessagingTraceContext.Extract(ea.BasicProperties.Headers);

        // ActivityKind.Consumer is the receiving half of the Producer span RabbitMqPublisher opened.
        // Passing parentContext explicitly (3rd arg to StartActivity) is what makes this span a *child*
        // of that Producer span instead of the root of a brand new trace.
        using var consumeSpan = activitySource.StartActivity(
            $"Consume {consumerOptions.QueueName}", ActivityKind.Consumer, parentContext);

        consumeSpan?.SetTag("messaging.system", "rabbitmq");
        consumeSpan?.SetTag("messaging.source.name", ea.Exchange);
        consumeSpan?.SetTag("messaging.destination.name", consumerOptions.QueueName);
        consumeSpan?.SetTag("messaging.rabbitmq.routing_key", ea.RoutingKey);
        consumeSpan?.SetTag("messaging.message.id", ea.BasicProperties.MessageId);

        TMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<TMessage>(ea.Body.Span);
        }
        catch (JsonException ex)
        {
            // Malformed payload — requeuing would cause an infinite hot-loop; dead-letter instead.
            logger.LogError(
                ex,
                "Failed to deserialize {MessageType} from delivery tag {DeliveryTag}. Message will not be requeued.",
                typeof(TMessage).Name, ea.DeliveryTag);

            consumeSpan?.AddException(ex);
            consumeSpan?.SetStatus(ActivityStatusCode.Error, "Malformed payload; dead-lettered.");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        if (message is null)
        {
            logger.LogWarning(
                "Received null payload for {MessageType} on delivery tag {DeliveryTag}. Message will not be requeued.",
                typeof(TMessage).Name, ea.DeliveryTag);

            consumeSpan?.AddEvent(new ActivityEvent("Null payload; dead-lettered."));
            consumeSpan?.SetStatus(ActivityStatusCode.Error, "Null payload; dead-lettered.");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
            // Because we're still inside the `using var consumeSpan` block, Activity.Current is
            // consumeSpan for the duration of this call — any span the handler starts (e.g. "Process
            // OrderCreated") is automatically parented to it without needing to pass context explicitly.
            var result = await handler.HandleAsync(message, ct);

            if (result.IsSuccess)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                consumeSpan?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                logger.LogWarning(
                    "Handler for {MessageType} (delivery tag {DeliveryTag}) reported failure: {@Errors}. Message will be requeued.",
                    typeof(TMessage).Name, ea.DeliveryTag, result.Errors);

                consumeSpan?.AddEvent(new ActivityEvent("Handler reported failure; message requeued."));
                consumeSpan?.SetStatus(ActivityStatusCode.Error, "Handler reported failure.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception in handler for {MessageType} (delivery tag {DeliveryTag}). Message will be requeued.",
                typeof(TMessage).Name, ea.DeliveryTag);

            consumeSpan?.AddException(ex);
            consumeSpan?.SetStatus(ActivityStatusCode.Error, "Unhandled exception; message requeued.");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }
}
