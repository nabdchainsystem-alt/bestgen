using System.Text.Json.Serialization;

namespace bestgen.Services.Ai;

/// <summary>
/// Strict shape Claude is asked to return when extracting a supplier
/// invoice from a PDF or image. Property names are camelCase to match
/// the prompt schema.
/// </summary>
public sealed class ExtractedInvoice
{
    [JsonPropertyName("supplierName")]      public string? SupplierName { get; set; }
    [JsonPropertyName("supplierVatNumber")] public string? SupplierVatNumber { get; set; }
    [JsonPropertyName("supplierAddress")]   public string? SupplierAddress { get; set; }
    [JsonPropertyName("supplierPhone")]     public string? SupplierPhone { get; set; }

    [JsonPropertyName("invoiceNumber")]     public string? InvoiceNumber { get; set; }
    [JsonPropertyName("invoiceDate")]       public string? InvoiceDate { get; set; }
    [JsonPropertyName("currency")]          public string? Currency { get; set; }

    [JsonPropertyName("subtotal")]          public decimal? Subtotal { get; set; }
    [JsonPropertyName("vatAmount")]         public decimal? VatAmount { get; set; }
    [JsonPropertyName("grandTotal")]        public decimal? GrandTotal { get; set; }
    [JsonPropertyName("vatRate")]           public decimal? VatRate { get; set; }

    [JsonPropertyName("notes")]             public string? Notes { get; set; }

    [JsonPropertyName("lines")]             public List<ExtractedInvoiceLine> Lines { get; set; } = new();
}

public sealed class ExtractedInvoiceLine
{
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("quantity")]    public decimal? Quantity { get; set; }
    [JsonPropertyName("unitPrice")]   public decimal? UnitPrice { get; set; }
    [JsonPropertyName("vatRate")]     public decimal? VatRate { get; set; }
    [JsonPropertyName("lineTotal")]   public decimal? LineTotal { get; set; }
}
