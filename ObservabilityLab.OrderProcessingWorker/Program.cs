using ObservabilityLab.OrderProcessingWorker.Services;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Messaging.Extensions;
using ObservabilityLab.Shared.Observability;
using Serilog;

Log.Logger = ObservabilityExtensions.CreateStartupLogger();
var builder = Host.CreateApplicationBuilder(args);

try
{
    Log.Information("Starting OrderProcessingWorker...");

    builder.Services.AddSharedDatabase(builder.Configuration);
    builder.Services.AddSharedMessaging(builder.Configuration);
    builder.Services.AddRabbitMqConsumer<OrderCreated, OrderCreatedMessageHandler>(RabbitMqTopology.Queues.OrderProcessingWorker);
    builder.Services.AddObservability(builder.Configuration, "ObservabilityLab.OrderProcessingWorker");

    var host = builder.Build();

    // Declare RabbitMQ topology idempotently on startup
    await host.Services.EnsureRabbitMqTopologyAsync();

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrderProcessingWorker start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
