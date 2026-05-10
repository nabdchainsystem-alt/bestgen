using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

/// <summary>
/// Single-line subscription / retainer invoice that auto-generates on a schedule.
/// Multi-line recurring templates can be modelled as multiple records pointing
/// at the same customer with the same NextRunDate; the generator groups them.
/// </summary>
public class RecurringInvoice
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }

    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; } = 15m;
    public decimal Discount { get; set; }

    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;

    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime NextRunDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastGeneratedAt { get; set; }
    public int? LastGeneratedInvoiceId { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public Customer? Customer { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Product? Product { get; set; }
}
