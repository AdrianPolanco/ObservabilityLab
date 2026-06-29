

using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text.Json;

namespace ObservabilityLab.Shared.Messaging
{
    public class RabbitMqPublisher(RabbitMqChannelPool channelPool, ILogger<RabbitMqPublisher> logger)
    {
        public async Task<bool> PublishAsync<T>(string exchange, string routingKey, T message, CancellationToken cancellationToken)
        {
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
                Timestamp = new(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await channelLease.Channel.BasicPublishAsync(exchange, routingKey, true, basicProps, json, cancellationToken);

            logger.LogInformation("{@MessagePublishedData}",
                new { Details = "Message published successfully.", 
                    Exchange = exchange, 
                    RoutingKey = routingKey,
                    basicProps.MessageId, 
                    basicProps.ContentType,
                    basicProps.Timestamp
                });

            return true;
        }
    }
}
