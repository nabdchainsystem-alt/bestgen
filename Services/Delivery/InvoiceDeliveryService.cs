using bestgen.Data;
using bestgen.Models;
using bestgen.Services.InvoicePdf;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Delivery;

/// <summary>
/// Coordinates invoice delivery: render PDF → send via the requested channel
/// → write a row to <see cref="InvoiceDeliveryLog"/> regardless of outcome.
/// </summary>
public class InvoiceDeliveryService
{
    private readonly ApplicationDbContext _db;
    private readonly InvoicePdfService _pdf;
    private readonly EmailDeliveryService _email;
    private readonly WhatsAppDeliveryService _whatsapp;

    public InvoiceDeliveryService(
        ApplicationDbContext db,
        InvoicePdfService pdf,
        EmailDeliveryService email,
        WhatsAppDeliveryService whatsapp)
    {
        _db = db;
        _pdf = pdf;
        _email = email;
        _whatsapp = whatsapp;
    }

    public bool EmailConfigured => _email.IsConfigured;
    public bool WhatsAppConfigured => _whatsapp.IsConfigured;
    public bool WhatsAppTemplateConfigured => _whatsapp.TemplateConfigured;

    public async Task<DeliveryResult> SendSalesInvoiceAsync(
        int invoiceId, DeliveryChannel channel, string recipient, string? userId,
        bool useTemplate = false, CancellationToken ct = default)
    {
        var rendered = await _pdf.RenderSalesInvoiceAsync(invoiceId);
        if (rendered is null) return DeliveryResult.Fail("Invoice not found.");

        var invoice = await _db.SalesInvoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .FirstAsync(x => x.Id == invoiceId, ct);

        var subject = $"Invoice {invoice.InvoiceNumber}";
        var body =
            $"Hello,\n\nPlease find attached invoice {invoice.InvoiceNumber} dated {invoice.InvoiceDate:yyyy-MM-dd}, total {invoice.GrandTotal:N2}.\n\nThank you.";

        return await SendAsync(
            DeliveryDocumentType.SalesInvoice, invoiceId, invoice.InvoiceNumber,
            channel, recipient, rendered.Value.Bytes, rendered.Value.FileName,
            subject, body, invoice.GrandTotal, userId, useTemplate, ct);
    }

    public async Task<DeliveryResult> SendPurchaseInvoiceAsync(
        int invoiceId, DeliveryChannel channel, string recipient, string? userId,
        bool useTemplate = false, CancellationToken ct = default)
    {
        var rendered = await _pdf.RenderPurchaseInvoiceAsync(invoiceId);
        if (rendered is null) return DeliveryResult.Fail("Invoice not found.");

        var invoice = await _db.PurchaseInvoices
            .AsNoTracking()
            .FirstAsync(x => x.Id == invoiceId, ct);

        var subject = $"Purchase invoice {invoice.PurchaseInvoiceNumber}";
        var body =
            $"Hello,\n\nForwarding purchase invoice {invoice.PurchaseInvoiceNumber} dated {invoice.InvoiceDate:yyyy-MM-dd}, total {invoice.GrandTotal:N2}.\n\nThank you.";

        return await SendAsync(
            DeliveryDocumentType.PurchaseInvoice, invoiceId, invoice.PurchaseInvoiceNumber,
            channel, recipient, rendered.Value.Bytes, rendered.Value.FileName,
            subject, body, invoice.GrandTotal, userId, useTemplate, ct);
    }

    private async Task<DeliveryResult> SendAsync(
        DeliveryDocumentType type, int docId, string docNumber,
        DeliveryChannel channel, string recipient, byte[] pdf, string fileName,
        string subject, string body, decimal amount, string? userId, bool useTemplate, CancellationToken ct)
    {
        bool ok; string? err; string? msgId;
        if (channel == DeliveryChannel.Email)
        {
            (ok, err, msgId) = await _email.SendAsync(recipient, subject, body, pdf, fileName, ct);
        }
        else if (useTemplate)
        {
            (ok, err, msgId) = await _whatsapp.SendInvoiceTemplateAsync(recipient, pdf, fileName, docNumber, amount.ToString("N2"), ct);
        }
        else
        {
            var caption = $"{subject}\n{body}";
            (ok, err, msgId) = await _whatsapp.SendDocumentAsync(recipient, pdf, fileName, caption, ct);
        }

        var log = new InvoiceDeliveryLog
        {
            DocumentType = type,
            DocumentId = docId,
            DocumentNumber = docNumber,
            Channel = channel,
            Recipient = recipient,
            Status = ok ? DeliveryStatus.Sent : DeliveryStatus.Failed,
            ErrorMessage = err,
            ProviderMessageId = msgId,
            SentByUserId = userId,
            SentAt = DateTime.UtcNow
        };
        _db.InvoiceDeliveryLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        bestgen.Services.Observability.BestgenMetrics.DeliveryAttempts
            .WithLabels(channel == DeliveryChannel.Email ? "email" : "whatsapp", ok ? "ok" : "fail")
            .Inc();
        return ok ? DeliveryResult.Ok(msgId) : DeliveryResult.Fail(err ?? "Unknown error");
    }
}

public sealed record DeliveryResult(bool Success, string? Error, string? MessageId)
{
    public static DeliveryResult Ok(string? id) => new(true, null, id);
    public static DeliveryResult Fail(string err) => new(false, err, null);
}
