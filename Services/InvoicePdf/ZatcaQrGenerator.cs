using System.Globalization;
using System.Text;
using QRCoder;

namespace bestgen.Services.InvoicePdf;

/// <summary>
/// Generates a ZATCA Phase 1 compliant QR code for an invoice — TLV-encoded
/// (tag/length/value) with the five required fields, base64'd, then PNG-rendered.
///
/// Phase 1 (Generation phase) is satisfied by a TLV-encoded QR that scans to:
///   1: Seller name
///   2: VAT registration number
///   3: Timestamp (ISO 8601 UTC)
///   4: Invoice total (with VAT)
///   5: VAT amount
///
/// Phase 2 adds a cryptographic stamp + UUID + invoice hash + signed XML —
/// implemented in a later iteration.
/// </summary>
public static class ZatcaQrGenerator
{
    public static byte[] GenerateQrPng(
        string sellerName,
        string vatNumber,
        DateTime timestamp,
        decimal totalWithVat,
        decimal vatTotal,
        int pixelsPerModule = 16)
    {
        var base64 = BuildTlvBase64(sellerName, vatNumber, timestamp, totalWithVat, vatTotal);
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(base64, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    public static string BuildTlvBase64(
        string sellerName,
        string vatNumber,
        DateTime timestamp,
        decimal totalWithVat,
        decimal vatTotal)
    {
        using var ms = new MemoryStream();
        WriteTlv(ms, 1, sellerName ?? string.Empty);
        WriteTlv(ms, 2, vatNumber ?? string.Empty);
        WriteTlv(ms, 3, timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        WriteTlv(ms, 4, totalWithVat.ToString("F2", CultureInfo.InvariantCulture));
        WriteTlv(ms, 5, vatTotal.ToString("F2", CultureInfo.InvariantCulture));
        return Convert.ToBase64String(ms.ToArray());
    }

    private static void WriteTlv(MemoryStream ms, byte tag, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > 255)
        {
            // ZATCA values shouldn't exceed 255 bytes; truncate defensively.
            bytes = bytes.Take(255).ToArray();
        }
        ms.WriteByte(tag);
        ms.WriteByte((byte)bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }
}
