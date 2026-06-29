using ObservabilityLab.EmailWorker;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddSharedDatabase(builder.Configuration);
builder.Services.AddSharedMessaging(builder.Configuration);

var host = builder.Build();

// Declare RabbitMQ topology idempotently on startup
await host.Services.EnsureRabbitMqTopologyAsync();

host.Run();
