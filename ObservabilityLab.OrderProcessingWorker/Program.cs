using ObservabilityLab.OrderProcessingWorker.Services;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Messaging.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSharedDatabase(builder.Configuration);
builder.Services.AddSharedMessaging(builder.Configuration);
builder.Services.AddRabbitMqConsumer<OrderCreated, OrderCreatedMessageHandler>(RabbitMqTopology.Queues.OrderProcessingWorker);

var host = builder.Build();

// Declare RabbitMQ topology idempotently on startup
await host.Services.EnsureRabbitMqTopologyAsync();

host.Run();
