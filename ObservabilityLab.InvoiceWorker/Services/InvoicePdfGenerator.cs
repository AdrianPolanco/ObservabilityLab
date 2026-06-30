using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ObservabilityLab.InvoiceWorker.Services;

internal interface IInvoicePdfGenerator
{
    byte[] Generate(InvoiceDto invoice);
}

internal sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    public byte[] Generate(InvoiceDto invoice) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                // ── Header ────────────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Text("INVOICE").FontSize(24).Bold();
                    col.Item().Text($"Order:  {invoice.Order.Id}");
                    col.Item().Text($"Date:   {invoice.IssuedAt:yyyy-MM-dd}");
                });

                // ── Content ───────────────────────────────────────────────────────
                page.Content().PaddingVertical(20).Column(col =>
                {
                    // Bill-to block
                    col.Item().PaddingBottom(16).Column(billTo =>
                    {
                        billTo.Item().Text("BILL TO").Bold();
                        billTo.Item().Text(invoice.CustomerName);
                        billTo.Item().Text(invoice.CustomerEmail);
                    });

                    // Items table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4); // Product name
                            cols.RelativeColumn(2); // Unit price
                            cols.RelativeColumn(1); // Qty
                            cols.RelativeColumn(2); // Line total
                        });

                        // Column headers
                        table.Header(header =>
                        {
                            header.Cell().Text("Product").Bold();
                            header.Cell().AlignRight().Text("Unit Price").Bold();
                            header.Cell().AlignRight().Text("Qty").Bold();
                            header.Cell().AlignRight().Text("Line Total").Bold();
                        });

                        // Item rows
                        foreach (var item in invoice.Items)
                        {
                            var lineTotal = item.Product.UnitPrice * item.Quantity;

                            table.Cell().Text(item.Product.ProductName);
                            table.Cell().AlignRight().Text(item.Product.UnitPrice.ToString("N2"));
                            table.Cell().AlignRight().Text(item.Quantity.ToString());
                            table.Cell().AlignRight().Text(lineTotal.ToString("N2"));
                        }

                        // Total row
                        table.Cell().ColumnSpan(3).AlignRight().Text("TOTAL").Bold();
                        table.Cell().AlignRight().Text(invoice.TotalAmount.ToString("N2")).Bold();
                    });
                });

                // ── Footer ────────────────────────────────────────────────────────
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
}
