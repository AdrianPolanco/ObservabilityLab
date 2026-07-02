using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using ObservabilityLab.Shared.Services;
using System.Diagnostics;
using static ObservabilityLab.Shared.Services.MinIOConstants;

namespace ObservabilityLab.EmailWorker.Services
{
    internal class InvoiceGeneratedMessageHandler(
        ApplicationDbContext dbContext,
        EmailSender emailSender,
        RabbitMqPublisher publisher,
        MinIOService minIOService,
        ILogger<InvoiceGeneratedMessageHandler> logger,
        ActivitySource activitySource) : IMessageHandler<InvoiceGenerated>
    {
        public async Task<Result<InvoiceGenerated>> HandleAsync(InvoiceGenerated message, CancellationToken cancellationToken)
        {
            // Last hop of the pipeline: nests under the "Consume email-worker" Consumer span, which is a
            // child of InvoiceWorker's "Publish Message" span — same trace-id all the way from the
            // original POST /orders down to this email send.
            using var processSpan = activitySource.StartActivity("Process InvoiceGenerated", ActivityKind.Internal);
            processSpan?.SetTag("order.id", message.OrderId);
            processSpan?.SetTag("invoice.id", message.InvoiceId);

            var receivedMessageLog = new
            {
                Status = "ReceivedMessage",
                Message = message
            };

            logger.LogInformation("{@Received}", receivedMessageLog);

            // minIOService.GetFileAsync's HTTP call to MinIO is captured automatically as a Client span
            // by OpenTelemetry.Instrumentation.Http.
            var file = await minIOService.GetFileAsync(message.FilePath, Buckets.Invoices, cancellationToken);

            var emailAddress = await dbContext.Orders
                .Where(o => o.Id == message.OrderId)
                .Join(dbContext.Customers, o => o.CustomerId, c => c.Id, (o, c) => c.Email)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            // emailSender.SendEmailAsync opens its own "SMTP send" Client span (MailKit has no
            // auto-instrumentation, so that one is hand-written — see EmailSender).
            var sent = await emailSender.SendEmailAsync(emailAddress, "The invoice for your order", message.FilePath, file, cancellationToken);

            if (!sent)
            {
                logger.LogWarning("Failed to send invoice email for order {OrderId} to {EmailAddress}.", message.OrderId, emailAddress);
                processSpan?.SetStatus(ActivityStatusCode.Error, "Invoice email send failed");
                return Result<InvoiceGenerated>
                    .Failure(new("EmailNotSent", $"The email for the invoice of the order {message.OrderId} could not be sent.", new() {
                        { "OrderId", message.OrderId },
                        { "EmailAddress", emailAddress }
                    }));
            }

            var invoice = await dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == message.InvoiceId, cancellationToken);

            invoice.MarkEmailAsSent();

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Invoice email sent for order {OrderId}, invoice {InvoiceId}.", message.OrderId, message.InvoiceId);
            processSpan?.SetStatus(ActivityStatusCode.Ok);

            return Result<InvoiceGenerated>.Success(message);
        }
    }
}
