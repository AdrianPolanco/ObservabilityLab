using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Entities;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using ObservabilityLab.Shared.Services;
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
    ILogger<OrderProcessedMessageHandler> logger) : IMessageHandler<OrderProcessed>
{
    public async Task<Result<OrderProcessed>> HandleAsync(OrderProcessed message, CancellationToken cancellationToken)
    {
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
            return Result<OrderProcessed>.Failure(
                new Error("OrderNotFound", $"Order {message.OrderId} not found while building invoice.", new() {
                    { "OrderId", message.OrderId }
                }));
        }

        var pdfBytes = pdfGenerator.Generate(invoiceDto);

        var objectName = $"{message.OrderId}.pdf";
        var (isSuccess, _) = await minioService.UploadAsync(
            objectName, pdfBytes, MinIOConstants.Buckets.Invoices, "application/pdf", cancellationToken);

        if (!isSuccess)
        {
            logger.LogWarning("Failed to upload invoice PDF for order {OrderId} as {ObjectName}.", message.OrderId, objectName);
            return Result<OrderProcessed>.Failure(
                new Error("InvoiceUploadFailed", $"Failed to upload invoice PDF for order {message.OrderId}.", new() {
                    { "OrderId", message.OrderId },
                    { "ObjectName", objectName }
                }));
        }

        logger.LogInformation(
            "Uploaded invoice PDF for order {OrderId} as {ObjectName}.",
            message.OrderId, objectName);

        var invoiceResult = Invoice.Create(invoiceDto.Order, objectName);

        if (invoiceResult.Errors.Any())
        {
            logger.LogWarning("Invoice validation failed for order {OrderId}: {@Errors}", message.OrderId, invoiceResult.Errors);
            return Result<OrderProcessed>.Failures(invoiceResult.Errors.ToList());
        }

        var invoice = invoiceResult.Data;

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
            return Result<OrderProcessed>.Failure(new Error("InvoiceMessageUnpublished", $"The invoice generation for {invoice.Id} could not be published.", new() {
                { "InvoiceId", invoice.Id },
                { "OrderId", invoiceDto.Order.Id }
            }));
        }

        logger.LogInformation("Invoice {InvoiceId} generated and InvoiceGenerated event published for order {OrderId}.", invoice.Id, invoiceDto.Order.Id);

        return Result<OrderProcessed>.Success(message);
    }
}
