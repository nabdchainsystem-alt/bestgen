namespace bestgen.Services.Attachments;

/// <summary>
/// Detects spoofed uploads — checks the first few bytes ("magic numbers")
/// of an uploaded stream against the declared content type. Stops a user from
/// renaming evil.exe to invoice.pdf and slipping it past the extension filter.
/// </summary>
public static class FileSignatureValidator
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"]  = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },                                // "%PDF"
        [".png"]  = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        [".jpg"]  = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
        [".gif"]  = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } },                                // "GIF8"
        [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } },                                // "RIFF"
        [".bmp"]  = new[] { new byte[] { 0x42, 0x4D } },                                            // "BM"
        [".zip"]  = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 } },
        [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },                                // also a zip
        [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        [".doc"]  = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
        [".xls"]  = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },
    };

    /// <summary>
    /// Returns true if the head of <paramref name="stream"/> matches one of the
    /// known signatures for <paramref name="extension"/>. Resets the stream to
    /// position 0 so the caller can still copy from it.
    /// Extensions not in the table return true (no signature to verify against).
    /// </summary>
    public static bool Validate(Stream stream, string extension)
    {
        if (!Signatures.TryGetValue(extension, out var sigs))
        {
            // CSV / TXT / HEIC and friends — no reliable magic bytes; allow.
            return true;
        }

        var maxLen = sigs.Max(s => s.Length);
        var head = new byte[maxLen];
        var read = stream.Read(head, 0, maxLen);
        stream.Seek(0, SeekOrigin.Begin);
        if (read < maxLen) return false;

        return sigs.Any(sig => head.Take(sig.Length).SequenceEqual(sig));
    }
}
