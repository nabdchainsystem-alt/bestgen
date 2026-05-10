namespace bestgen.Services.InvoicePdf;

/// <summary>
/// Provider-neutral data shape passed to the QuestPDF document. Both
/// <see cref="bestgen.Models.SalesInvoice"/> and
/// <see cref="bestgen.Models.PurchaseInvoice"/> are projected into this so
/// the same template renders for both.
/// </summary>
public sealed class InvoicePdfModel
{
    // --- meta ---
    public required string DocumentTitle { get; init; }   // "Tax Invoice" / "فاتورة ضريبية"
    public required string DocumentNumber { get; init; }
    public DateTime DocumentDate { get; init; }
    public string? StatusLabel { get; init; }

    // --- seller (your company) ---
    public required string SellerName { get; init; }
    public string? SellerNameEn { get; init; }
    public string? SellerVatNumber { get; init; }
    public string? SellerCommercialRegistration { get; init; }
    public string? SellerAddress { get; init; }
    public string? SellerCity { get; init; }
    public string? SellerCountry { get; init; }
    public string? SellerLogoPath { get; init; }

    // --- counterparty (customer for sales, supplier for purchases) ---
    public required string CounterpartyHeading { get; init; }   // "Bill To" / "إلى السادة"
    public required string CounterpartyName { get; init; }
    public string? CounterpartyVatNumber { get; init; }
    public string? CounterpartyAddress { get; init; }
    public string? CounterpartyPhone { get; init; }

    // --- additional context ---
    public string? Warehouse { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Notes { get; init; }
    public string? FooterText { get; init; }

    // --- lines ---
    public required IReadOnlyList<InvoicePdfLine> Lines { get; init; }

    // --- totals ---
    public decimal Subtotal { get; init; }
    public decimal DiscountTotal { get; init; }
    public decimal VatTotal { get; init; }
    public decimal GrandTotal { get; init; }
    public required string CurrencySymbol { get; init; } = "ر.س";

    // --- ZATCA Phase 1 ---
    public bool IncludeZatcaQr { get; init; } = true;
}

public sealed class InvoicePdfLine
{
    public required string ProductName { get; init; }
    public string? ProductSku { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Discount { get; init; }
    public decimal VatRate { get; init; }
    public decimal LineTotal { get; init; }
}
