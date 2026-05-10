using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public enum AttachmentDocumentType
{
    SalesInvoice,
    PurchaseInvoice,
    Expense,
    JournalEntry,
    Employee,
    SupplierPayment,
    SalesReceipt,
    FixedAsset
}

public class Attachment
{
    public int Id { get; set; }

    public AttachmentDocumentType DocumentType { get; set; }
    public int DocumentId { get; set; }

    [Required, StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [StringLength(120)]
    public string? ContentType { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Path on disk (relative to App_Data/uploads), opaque to callers.</summary>
    [Required, StringLength(400)]
    public string StoragePath { get; set; } = string.Empty;

    [StringLength(160)]
    public string? UploadedByUserId { get; set; }
    [StringLength(160)]
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Description { get; set; }
}
