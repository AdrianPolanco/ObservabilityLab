using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ObservabilityLab.Shared.Messaging.Extensions;

public static class MessagingSharedExtensions
{
    /// <summary>
    /// Registers RabbitMQ configuration options and the shared <see cref="IConnectionFactory"/>
    /// in the DI container. Call this alongside <c>AddSharedDatabase</c> in every startup project.
    /// </summary>
    public static IServiceCollection AddSharedMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost,
            };
        });

        return services;
    }

    /// <summary>
    /// Declares the RabbitMQ topology (exchange, queues, bindings) defined in
    /// <see cref="RabbitMqTopology"/> idempotently. Safe to call on every startup
    /// because AMQP Declare operations are no-ops when the entity already exists
    /// with identical parameters.
    /// Retries the broker connection a few times to tolerate startup ordering when
    /// the container is not yet healthy.
    /// </summary>
    public static async Task EnsureRabbitMqTopologyAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IConnectionFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RabbitMqTopologyBootstrapper>>();

        const int maxAttempts = 5;
        const int retryDelaySeconds = 3;

        IConnection? connection = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                connection = await factory.CreateConnectionAsync(cancellationToken);
                break;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    "RabbitMQ not ready (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s...",
                    attempt, maxAttempts, ex.Message, retryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
            }
        }

        if (connection is null)
        {
            logger.LogError(
                "Could not connect to RabbitMQ after {Max} attempts. Topology was NOT declared.",
                maxAttempts);
            return;
        }

        await using (connection)
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await using (channel)
            {
                // Declare exchange
                await channel.ExchangeDeclareAsync(
                    exchange: RabbitMqTopology.Exchanges.OrderEvents,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken);

                logger.LogInformation(
                    "RabbitMQ exchange declared: {Exchange} (topic, durable)",
                    RabbitMqTopology.Exchanges.OrderEvents);

                // Declare queues and bindings
                foreach (var (queue, routingKey) in RabbitMqTopology.Bindings)
                {
                    await channel.QueueDeclareAsync(
                        queue: queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        cancellationToken: cancellationToken);

                    logger.LogInformation("RabbitMQ queue declared: {Queue} (durable)", queue);

                    await channel.QueueBindAsync(
                        queue: queue,
                        exchange: RabbitMqTopology.Exchanges.OrderEvents,
                        routingKey: routingKey,
                        cancellationToken: cancellationToken);

                    logger.LogInformation(
                        "RabbitMQ binding declared: {Exchange} --[{RoutingKey}]--> {Queue}",
                        RabbitMqTopology.Exchanges.OrderEvents, routingKey, queue);
                }
            }
        }

        logger.LogInformation("RabbitMQ topology bootstrap complete.");
    }

    /// <summary>Marker type used only to scope the topology-bootstrap logger.</summary>
    private sealed class RabbitMqTopologyBootstrapper;
}
