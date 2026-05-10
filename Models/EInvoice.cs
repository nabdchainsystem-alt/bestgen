using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

/// <summary>
/// ZATCA Phase 2 e-invoice artifact bound to a <see cref="SalesInvoice"/>.
/// Stores everything the integration phase requires: UBL 2.1 XML, the SHA-256
/// invoice hash, the previous invoice hash (chain), the cryptographic stamp,
/// the signed XML, and the Phase 2 9-field TLV QR base64.
/// </summary>
public class EInvoice
{
    public int Id { get; set; }

    public int SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    [Required, StringLength(40)]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>
    /// Invoice Counter Value — the seller-side sequential counter. ZATCA
    /// requires this to be monotonically increasing per seller.
    /// </summary>
    public long IcvCounter { get; set; }

    /// <summary>
    /// Previous Invoice Hash. The very first invoice uses the SHA-256 of "0"
    /// (a base64-encoded zero byte) as defined by ZATCA.
    /// </summary>
    [StringLength(120)]
    public string PreviousInvoiceHash { get; set; } = string.Empty;

    /// <summary>SHA-256 of the canonicalized UBL XML, base64.</summary>
    [StringLength(120)]
    public string InvoiceHash { get; set; } = string.Empty;

    /// <summary>"388" for Tax Invoice, "381" for Credit Note, "383" for Debit Note.</summary>
    [StringLength(8)]
    public string InvoiceTypeCode { get; set; } = "388";

    /// <summary>4-digit subtype: 1010 = standard B2B, 0200 = simplified B2C.</summary>
    [StringLength(8)]
    public string InvoiceSubtype { get; set; } = "0200";

    [StringLength(40)]
    public string ProfileId { get; set; } = "reporting:1.0";

    /// <summary>Unsigned UBL 2.1 XML.</summary>
    public string? Xml { get; set; }

    /// <summary>UBL 2.1 XML with the cryptographic stamp embedded in the
    /// UBLExtensions element.</summary>
    public string? SignedXml { get; set; }

    /// <summary>Base64 ECDSA P-256 signature over the invoice hash.</summary>
    [StringLength(600)]
    public string? Signature { get; set; }

    /// <summary>SHA-256 of the signing certificate (DER), base64.</summary>
    [StringLength(120)]
    public string? CertificateThumbprint { get; set; }

    /// <summary>The 9-field Phase 2 TLV QR as base64.</summary>
    public string? QrBase64 { get; set; }

    public EInvoiceStatus Status { get; set; } = EInvoiceStatus.Generated;

    /// <summary>Last response from FATOORA, when integrated.</summary>
    public string? FatooraResponse { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ClearedAt { get; set; }
}

public enum EInvoiceStatus
{
    Draft,
    Generated,
    Signed,
    Submitted,
    Cleared,
    Reported,
    Failed
}
