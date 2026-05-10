namespace bestgen.Models;

public enum DeliveryDocumentType
{
    SalesInvoice,
    PurchaseInvoice
}

public enum DeliveryChannel
{
    Email,
    WhatsApp
}

public enum DeliveryStatus
{
    Sent,
    Failed
}

public class InvoiceDeliveryLog
{
    public int Id { get; set; }

    public DeliveryDocumentType DocumentType { get; set; }
    public int DocumentId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;

    public DeliveryChannel Channel { get; set; }
    public string Recipient { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProviderMessageId { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? SentByUserId { get; set; }
}
