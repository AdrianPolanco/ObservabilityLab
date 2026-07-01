
using ObservabilityLab.EmailWorker;
using ObservabilityLab.EmailWorker.Options;
using ObservabilityLab.EmailWorker.Services;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Messaging.Extensions;
using ObservabilityLab.Shared.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSharedDatabase(builder.Configuration);
builder.Services.AddSharedMessaging(builder.Configuration);
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSharedServices();
builder.Services.AddScoped<EmailSender>();
builder.Services.AddRabbitMqConsumer<InvoiceGenerated, InvoiceGeneratedMessageHandler>(RabbitMqTopology.Queues.EmailWorker);

var host = builder.Build();

// Declare RabbitMQ topology idempotently on startup
await host.Services.EnsureRabbitMqTopologyAsync();

host.Run();
