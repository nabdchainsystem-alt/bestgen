using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public enum ApprovalDocumentType
{
    SalesInvoice,
    PurchaseInvoice,
    JournalEntry,
    SupplierPayment,
    Expense
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public class ApprovalPolicy
{
    public int Id { get; set; }

    public ApprovalDocumentType DocumentType { get; set; }

    /// <summary>
    /// Documents whose total amount is &gt;= this threshold require approval.
    /// 0 means every document of this type.
    /// </summary>
    public decimal MinAmount { get; set; } = 0m;

    public bool IsActive { get; set; } = true;

    [StringLength(80)]
    public string? RequiredRole { get; set; }

    /// <summary>
    /// 1-based ordering for multi-step approval chains. When several active
    /// policies match the same DocumentType + threshold, the request walks
    /// through them by SequenceOrder ascending — each must approve before
    /// the next is asked.
    /// </summary>
    public int SequenceOrder { get; set; } = 1;
}

public class ApprovalRequest
{
    public int Id { get; set; }

    public ApprovalDocumentType DocumentType { get; set; }
    public int DocumentId { get; set; }

    [StringLength(64)]
    public string DocumentNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    [StringLength(160)]
    public string? RequestedByUserId { get; set; }
    [StringLength(160)]
    public string? RequestedByName { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [StringLength(160)]
    public string? ResolvedByUserId { get; set; }
    [StringLength(160)]
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    /// <summary>1-based current step the request is waiting on.</summary>
    public int CurrentStep { get; set; } = 1;
    /// <summary>Total approval steps decided when the request was submitted.</summary>
    public int TotalSteps { get; set; } = 1;

    /// <summary>Newline-separated audit of each step's resolution.</summary>
    [StringLength(4000)]
    public string? StepHistory { get; set; }
}
