
using ObservabilityLab.EmailWorker;
using ObservabilityLab.EmailWorker.Options;
using ObservabilityLab.EmailWorker.Services;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Messaging.Extensions;
using ObservabilityLab.Shared.Observability;
using ObservabilityLab.Shared.Services;
using Serilog;

Log.Logger = ObservabilityExtensions.CreateStartupLogger();
var builder = Host.CreateApplicationBuilder(args);

try
{
    Log.Information("Starting EmailWorker...");

    builder.Services.AddSharedDatabase(builder.Configuration);
    builder.Services.AddSharedMessaging(builder.Configuration);
    builder.Services.AddOptions<SmtpOptions>()
        .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
        .ValidateOnStart();
    builder.Services.AddSharedServices();
    builder.Services.AddScoped<EmailSender>();
    builder.Services.AddRabbitMqConsumer<InvoiceGenerated, InvoiceGeneratedMessageHandler>(RabbitMqTopology.Queues.EmailWorker);
    builder.Services.AddObservability(builder.Configuration);

    var host = builder.Build();

    // Declare RabbitMQ topology idempotently on startup
    await host.Services.EnsureRabbitMqTopologyAsync();

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "EmailWorker start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
