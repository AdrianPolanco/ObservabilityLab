using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ObservabilityLab.Shared.Messaging.Extensions;

public static class MessagingSharedExtensions
{
    /// <summary>
    /// Registers RabbitMQ configuration options, the shared <see cref="IConnectionFactory"/>,
    /// the <see cref="RabbitMqConnectionManager"/> singleton, and the startup initialiser.
    /// Call this alongside <c>AddSharedDatabase</c> in every startup project.
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
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
            };
        });

        services.AddScoped<RabbitMqPublisher>();
        services.AddSingleton<RabbitMqConnectionManager>();
        services.AddSingleton<RabbitMqChannelPool>();
        services.AddHostedService<RabbitMqConnectionInitializer>();

        return services;
    }

    /// <summary>
    /// Declares the RabbitMQ topology (exchange, queues, bindings) defined in
    /// <see cref="RabbitMqTopology"/> idempotently. Safe to call on every startup
    /// because AMQP Declare operations are no-ops when the entity already exists
    /// with identical parameters.
    /// Reuses the process-wide connection from <see cref="RabbitMqConnectionManager"/>
    /// (including its startup retry logic) rather than opening a separate connection.
    /// </summary>
    public static async Task EnsureRabbitMqTopologyAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<RabbitMqConnectionManager>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RabbitMqTopologyBootstrapper>>();

        // Connection creation (including retries) is delegated to the manager.
        // Do NOT dispose the connection here — it is the shared singleton connection.
        var connection = await manager.GetConnectionAsync(cancellationToken);

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

        logger.LogInformation("RabbitMQ topology bootstrap complete.");
    }

    /// <summary>
    /// Registers a <see cref="RabbitMqConsumer{TMessage}"/> as a hosted service together with
    /// its <typeparamref name="THandler"/> and the queue name it should consume from.
    /// <para>
    /// Call this after <see cref="AddSharedMessaging"/> in each startup project's
    /// <c>Program.cs</c>:
    /// <code>
    /// builder.Services.AddRabbitMqConsumer&lt;OrderCreated, OrderCreatedHandler&gt;(
    ///     RabbitMqTopology.Queues.OrderProcessingWorker);
    /// </code>
    /// </para>
    /// The handler is resolved from a per-message DI scope so it may safely depend on scoped
    /// services (e.g. <c>ApplicationDbContext</c>).
    /// </summary>
    public static IServiceCollection AddRabbitMqConsumer<TMessage, THandler>(
        this IServiceCollection services,
        string queueName)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddSingleton(new ConsumerOptions<TMessage> { QueueName = queueName });
        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        services.AddHostedService<RabbitMqConsumer<TMessage>>();
        return services;
    }

    /// <summary>Marker type used only to scope the topology-bootstrap logger.</summary>
    private sealed class RabbitMqTopologyBootstrapper;
}
