using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using ObservabilityLab.Shared.Services;
using static ObservabilityLab.Shared.Services.MinIOConstants;

namespace ObservabilityLab.EmailWorker.Services
{
    internal class InvoiceGeneratedMessageHandler(
        ApplicationDbContext dbContext,
        EmailSender emailSender, 
        RabbitMqPublisher publisher,
        MinIOService minIOService, 
        ILogger<InvoiceGeneratedMessageHandler> logger) : IMessageHandler<InvoiceGenerated>
    {
        public async Task<Result<InvoiceGenerated>> HandleAsync(InvoiceGenerated message, CancellationToken cancellationToken)
        {
            var receivedMessageLog = new
            {
                Status = "ReceivedMessage",
                Message = message
            };

            logger.LogInformation("{@Received}", receivedMessageLog);

            var file = await minIOService.GetFileAsync(message.FilePath, Buckets.Invoices, cancellationToken);

            var emailAddress = await dbContext.Orders
                .Where(o => o.Id == message.OrderId)
                .Join(dbContext.Customers, o => o.CustomerId, c => c.Id, (o, c) => c.Email)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var sent = await emailSender.SendEmailAsync(emailAddress, "The invoice for your order", message.FilePath, file, cancellationToken);

            if (!sent)
            {
                return Result<InvoiceGenerated>
                    .Failure(new("EmailNotSent", $"The email for the invoice of the order {message.OrderId} could not be sent."));
            }
                
            var invoice = await dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == message.InvoiceId, cancellationToken);

            invoice.MarkEmailAsSent();

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<InvoiceGenerated>.Success(message);
        }
    }
}
