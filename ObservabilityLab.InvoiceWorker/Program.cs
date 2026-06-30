using ObservabilityLab.InvoiceWorker.Services;
using ObservabilityLab.Shared.Database.Extensions;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Messaging.Extensions;
using ObservabilityLab.Shared.Services;
using QuestPDF.Infrastructure;

// QuestPDF requires the license type to be set before any document is generated.
QuestPDF.Settings.License = LicenseType.Community;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSharedDatabase(builder.Configuration);
builder.Services.AddSharedMessaging(builder.Configuration);
builder.Services.AddSharedServices();
builder.Services.AddSingleton<IInvoicePdfGenerator, InvoicePdfGenerator>();
builder.Services.AddRabbitMqConsumer<OrderProcessed, OrderProcessedMessageHandler>(RabbitMqTopology.Queues.InvoiceWorker);

var host = builder.Build();

// Declare RabbitMQ topology idempotently on startup.
await host.Services.EnsureRabbitMqTopologyAsync();

// Ensure the invoices bucket exists in MinIO before any message is processed.
using (var scope = host.Services.CreateScope())
{
    var minio = scope.ServiceProvider.GetRequiredService<MinIOService>();
    await minio.EnsureBucketAsync(MinIOConstants.Buckets.Invoices, CancellationToken.None);
}

host.Run();
