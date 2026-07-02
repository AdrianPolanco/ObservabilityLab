using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Entities;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using ObservabilityLab.Shared.Services;
using System.Diagnostics;
using static ObservabilityLab.Shared.Messaging.Constants.MessagingConstants;

namespace ObservabilityLab.InvoiceWorker.Services;

internal record ProductDto(string ProductName, decimal UnitPrice);
internal record ItemDto(ProductDto Product, int Quantity);
internal record InvoiceDto(
    Order Order,
    DateTime IssuedAt,
    string CustomerName,
    string CustomerEmail,
    List<ItemDto> Items,
    decimal TotalAmount);

internal class OrderProcessedMessageHandler(
    ApplicationDbContext dbContext,
    RabbitMqPublisher publisher,
    IInvoicePdfGenerator pdfGenerator,
    MinIOService minioService,
    ILogger<OrderProcessedMessageHandler> logger,
    ActivitySource activitySource) : IMessageHandler<OrderProcessed>
{
    public async Task<Result<OrderProcessed>> HandleAsync(OrderProcessed message, CancellationToken cancellationToken)
    {
        // Nests under the "Consume invoice-worker" Consumer span opened by RabbitMqConsumer<TMessage>,
        // which itself is a child of the OrderProcessingWorker's "Publish Message" span — same trace,
        // third service.
        using var processSpan = activitySource.StartActivity("Process OrderProcessed", ActivityKind.Internal);
        processSpan?.SetTag("order.id", message.OrderId);
        processSpan?.SetTag("order.total_amount", message.TotalAmount);

        logger.LogInformation("Processing OrderProcessed event for order {OrderId}.", message.OrderId);

        // Single round-trip: join orders → customers and project items+products inline.
        // UnitPrice comes from order_items (captured at order time), not from products.current price.
        var invoiceDto = await dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Id == message.OrderId)
            .Join(dbContext.Customers,
                  o => o.CustomerId,
                  c => c.Id,
                  (o, c) => new InvoiceDto(
                      o,
                      message.CreatedAt,
                      c.Name,
                      c.Email,
                      o.Items
                          .Join(dbContext.Products,
                                i => i.ProductId,
                                p => p.Id,
                                (i, p) => new ItemDto(new ProductDto(p.Name, i.UnitPrice), i.Quantity))
                          .ToList(),
                      o.TotalAmount))
            .FirstOrDefaultAsync(cancellationToken);

        if (invoiceDto is null)
        {
            logger.LogWarning("Order {OrderId} not found while building invoice.", message.OrderId);
            processSpan?.SetStatus(ActivityStatusCode.Error, "Order not found while building invoice");
            return Result<OrderProcessed>.Failure(
                new Error("OrderNotFound", $"Order {message.OrderId} not found while building invoice.", new() {
                    { "OrderId", message.OrderId }
                }));
        }

        // pdfGenerator.Generate opens its own "Generate Invoice PDF" Internal span (see
        // InvoicePdfGenerator) — nests one level deeper under processSpan.
        var pdfBytes = pdfGenerator.Generate(invoiceDto);

        var objectName = $"{message.OrderId}.pdf";
        // minioService.UploadAsync's HTTP call to MinIO is captured automatically as a Client span by
        // OpenTelemetry.Instrumentation.Http (registered in ObservabilityExtensions) — no manual span needed.
        var (isSuccess, _) = await minioService.UploadAsync(
            objectName, pdfBytes, MinIOConstants.Buckets.Invoices, "application/pdf", cancellationToken);

        if (!isSuccess)
        {
            logger.LogWarning("Failed to upload invoice PDF for order {OrderId} as {ObjectName}.", message.OrderId, objectName);
            processSpan?.SetStatus(ActivityStatusCode.Error, "Invoice PDF upload failed");
            return Result<OrderProcessed>.Failure(
                new Error("InvoiceUploadFailed", $"Failed to upload invoice PDF for order {message.OrderId}.", new() {
                    { "OrderId", message.OrderId },
                    { "ObjectName", objectName }
                }));
        }

        processSpan?.AddEvent(new ActivityEvent("Invoice PDF uploaded", tags: new ActivityTagsCollection { { "object_name", objectName } }));
        logger.LogInformation(
            "Uploaded invoice PDF for order {OrderId} as {ObjectName}.",
            message.OrderId, objectName);

        var invoiceResult = Invoice.Create(invoiceDto.Order, objectName);

        if (invoiceResult.Errors.Any())
        {
            logger.LogWarning("Invoice validation failed for order {OrderId}: {@Errors}", message.OrderId, invoiceResult.Errors);
            processSpan?.SetStatus(ActivityStatusCode.Error, "Invoice validation failed");
            return Result<OrderProcessed>.Failures(invoiceResult.Errors.ToList());
        }

        var invoice = invoiceResult.Data;
        processSpan?.SetTag("invoice.id", invoice.Id);

        await dbContext.Invoices.AddAsync(invoice, cancellationToken);
        await dbContext.Orders.Where(o => o.Id == message.OrderId).ExecuteUpdateAsync(o => o.SetProperty(o => o.Status, OrderStatus.Invoiced), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var published = await publisher.PublishAsync(
            Exchanges.OrderEvents,
            RoutingKeys.InvoiceGenerated,
            new InvoiceGenerated(invoice.Id, invoiceDto.Order.Id, objectName, DateTime.UtcNow),
            cancellationToken);

        if (!published)
        {
            logger.LogWarning("Failed to publish InvoiceGenerated event for invoice {InvoiceId} (order {OrderId}).", invoice.Id, invoiceDto.Order.Id);
            processSpan?.SetStatus(ActivityStatusCode.Error, "InvoiceGenerated publish failed");
            return Result<OrderProcessed>.Failure(new Error("InvoiceMessageUnpublished", $"The invoice generation for {invoice.Id} could not be published.", new() {
                { "InvoiceId", invoice.Id },
                { "OrderId", invoiceDto.Order.Id }
            }));
        }

        logger.LogInformation("Invoice {InvoiceId} generated and InvoiceGenerated event published for order {OrderId}.", invoice.Id, invoiceDto.Order.Id);
        processSpan?.SetStatus(ActivityStatusCode.Ok);

        return Result<OrderProcessed>.Success(message);
    }
}
