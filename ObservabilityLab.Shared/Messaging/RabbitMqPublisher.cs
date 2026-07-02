

using Microsoft.Extensions.Logging;
using ObservabilityLab.Shared.Observability;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text.Json;

namespace ObservabilityLab.Shared.Messaging
{
    public class RabbitMqPublisher(RabbitMqChannelPool channelPool, ILogger<RabbitMqPublisher> logger, ActivitySource activitySource)
    {
        public async Task<bool> PublishAsync<T>(string exchange, string routingKey, T message, CancellationToken cancellationToken)
        {
            // ActivityKind.Producer is the span-kind convention for "this call hands work off to be
            // processed asynchronously elsewhere" — Tempo/Grafana use it (paired with Consumer on the
            // other end) to draw the messaging edge in the trace/service graph.
            // StartActivity returns null when nothing is sampling (e.g. no listener registered, or the
            // sampler dropped it) — every call below uses the null-conditional operator so instrumentation
            // never breaks the actual publish.
            using var publishSpan = activitySource.StartActivity("Publish Message", ActivityKind.Producer);
            cancellationToken.ThrowIfCancellationRequested();

            var json = JsonSerializer.SerializeToUtf8Bytes(message);

            cancellationToken.ThrowIfCancellationRequested();

            var channelLease = await channelPool.AcquireAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var basicProps = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>()
            };

            // Stamp this span's trace context into the message headers *before* publishing, while
            // publishSpan is still Activity.Current. This is the hop that keeps the trace unbroken across
            // the queue — see MessagingTraceContext for why RabbitMQ needs this done by hand.
            MessagingTraceContext.Inject(basicProps.Headers);

            try
            {
                await channelLease.Channel.BasicPublishAsync(exchange, routingKey, true, basicProps, json, cancellationToken);
            }
            catch (Exception ex)
            {
                publishSpan?.AddException(ex);
                publishSpan?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }

            logger.LogInformation("{@MessagePublishedData}",
                new { Details = "Message published successfully.",
                    Exchange = exchange,
                    RoutingKey = routingKey,
                    basicProps.MessageId,
                    basicProps.ContentType,
                    basicProps.Timestamp
                });

            // Tags below follow OpenTelemetry's messaging semantic conventions
            // (https://opentelemetry.io/docs/specs/semconv/messaging/) — using the standard attribute
            // names means Tempo/Grafana (and any future backend) can recognize and group these spans as
            // messaging operations rather than opaque "Internal" work.
            publishSpan?.SetTag("messaging.system", "rabbitmq");
            publishSpan?.SetTag("messaging.destination.name", exchange);
            publishSpan?.SetTag("messaging.rabbitmq.routing_key", routingKey);
            publishSpan?.SetTag("messaging.message.id", basicProps.MessageId);
            publishSpan?.SetStatus(ActivityStatusCode.Ok, "Message published successfully.");
            return true;
        }
    }
}
