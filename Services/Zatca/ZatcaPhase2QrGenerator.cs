using System.Globalization;
using System.Text;
using QRCoder;

namespace bestgen.Services.Zatca;

/// <summary>
/// Generates the Phase 2 QR (TLV with 9 tags). Phase 1 only had 5; Phase 2
/// adds the invoice hash, signature, public key, and certificate signature.
///
///  1: Seller name
///  2: Seller VAT
///  3: Timestamp (ISO 8601)
///  4: Invoice total (with VAT)
///  5: VAT amount
///  6: Invoice hash (base64 SHA-256 of canonical XML)
///  7: ECDSA signature (base64)
///  8: Public key DER bytes (raw, not base64-text)
///  9: Certificate signature (sha256 of cert raw, raw bytes)
/// </summary>
public static class ZatcaPhase2QrGenerator
{
    public static (byte[] Png, string Base64) Generate(
        string sellerName,
        string sellerVat,
        DateTime timestamp,
        decimal totalWithVat,
        decimal vatTotal,
        string invoiceHashBase64,
        byte[] signatureBytes,
        byte[] publicKeyDer,
        byte[] certSignatureBytes,
        int pixelsPerModule = 14)
    {
        using var ms = new MemoryStream();
        WriteTextTlv(ms, 1, sellerName);
        WriteTextTlv(ms, 2, sellerVat);
        WriteTextTlv(ms, 3, timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        WriteTextTlv(ms, 4, totalWithVat.ToString("F2", CultureInfo.InvariantCulture));
        WriteTextTlv(ms, 5, vatTotal.ToString("F2", CultureInfo.InvariantCulture));
        WriteTextTlv(ms, 6, invoiceHashBase64);
        WriteBytesTlv(ms, 7, signatureBytes);
        WriteBytesTlv(ms, 8, publicKeyDer);
        WriteBytesTlv(ms, 9, certSignatureBytes);

        var bytes = ms.ToArray();
        var base64 = Convert.ToBase64String(bytes);

        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(base64, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return (qr.GetGraphic(pixelsPerModule), base64);
    }

    private static void WriteTextTlv(MemoryStream ms, byte tag, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        WriteBytesTlv(ms, tag, bytes);
    }

    private static void WriteBytesTlv(MemoryStream ms, byte tag, byte[] value)
    {
        // Tag (1 byte) + Length (1 byte for <=255, else extended) + Value.
        // For ZATCA, the cert/signature/pubkey can exceed 255 bytes — use the
        // long-form length encoding (one byte indicating length-of-length, then
        // the length bytes themselves) per the standard TLV spec they expect.
        ms.WriteByte(tag);
        if (value.Length <= 0xFF)
        {
            ms.WriteByte((byte)value.Length);
        }
        else
        {
            // ZATCA's reference impl uses a single-byte length, but their cert
            // payloads do exceed 255 bytes in practice. The deployed approach
            // is "BER-style" two-byte length: 0x82 then 16-bit big-endian length.
            ms.WriteByte(0x82);
            ms.WriteByte((byte)((value.Length >> 8) & 0xFF));
            ms.WriteByte((byte)(value.Length & 0xFF));
        }
        ms.Write(value, 0, value.Length);
    }
}
