using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bestgen.Models;

namespace bestgen.Services.Audit;

/// <summary>
/// Append-only file mirror of the in-DB audit log. Each line is a JSON object
/// with a <c>hash</c> field that's SHA-256 over the previous line's hash plus
/// the current row payload. Tampering with any line invalidates every hash
/// after it — surfaced by <see cref="VerifyAsync"/>.
///
/// Files rotate daily by UTC date. They live under <c>App_Data/audit/</c>
/// (gitignored) and survive any in-DB deletion of <c>AuditEntries</c>.
/// </summary>
public class AuditSink
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuditSink> _logger;
    private readonly SemaphoreSlim _sema = new(1, 1);

    private string _currentDate = string.Empty;
    private string? _lastHash;

    public AuditSink(IConfiguration config, ILogger<AuditSink> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool Enabled => _config.GetValue("Audit:Sink:Enabled", true);

    public string Directory =>
        _config.GetValue<string>("Audit:Sink:Directory")
        ?? Path.Combine(Environment.CurrentDirectory, "App_Data", "audit");

    public string FilePathFor(DateTime utcDate) =>
        Path.Combine(Directory, $"{utcDate:yyyy-MM-dd}.jsonl");

    public async Task WriteAsync(IReadOnlyCollection<AuditEntry> rows, CancellationToken ct = default)
    {
        if (!Enabled || rows.Count == 0) return;

        await _sema.WaitAsync(ct);
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var nowUtc = DateTime.UtcNow;
            var date = nowUtc.ToString("yyyy-MM-dd");
            var path = FilePathFor(nowUtc);

            // Re-load chain head when day rolls over (or on first call).
            if (date != _currentDate)
            {
                _currentDate = date;
                _lastHash = ReadLastHash(path);
            }

            await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(fs);
            foreach (var row in rows)
            {
                // Build the canonical payload with the previous hash, then compute this line's hash.
                var payload = new SortedDictionary<string, object?>
                {
                    ["ts"] = row.At.ToUniversalTime().ToString("o"),
                    ["user"] = row.UserName,
                    ["entity"] = row.EntityName,
                    ["key"] = row.EntityKey,
                    ["action"] = row.Action.ToString(),
                    ["summary"] = row.Summary,
                    ["prev_hash"] = _lastHash
                };
                var canonical = JsonSerializer.Serialize(payload);
                var hash = Sha256Hex(canonical);
                payload["hash"] = hash;
                await writer.WriteLineAsync(JsonSerializer.Serialize(payload));
                _lastHash = hash;
            }
            await writer.FlushAsync();
        }
        catch (Exception ex)
        {
            // Audit-sink failures are logged but never break the user's request.
            _logger.LogWarning(ex, "Audit sink write failed");
        }
        finally
        {
            _sema.Release();
        }
    }

    public IReadOnlyList<AuditFileInfo> ListFiles()
    {
        if (!System.IO.Directory.Exists(Directory)) return Array.Empty<AuditFileInfo>();
        return System.IO.Directory.GetFiles(Directory, "*.jsonl")
            .OrderByDescending(f => f)
            .Select(f =>
            {
                var fi = new FileInfo(f);
                int lines;
                try { lines = File.ReadLines(f).Count(); } catch { lines = 0; }
                return new AuditFileInfo(Path.GetFileName(f), fi.Length, fi.LastWriteTimeUtc, lines);
            })
            .ToList();
    }

    public async Task<byte[]> ReadFileAsync(string fileName, CancellationToken ct = default)
    {
        var safe = Path.GetFileName(fileName); // guard against ../
        var path = Path.Combine(Directory, safe);
        if (!File.Exists(path)) throw new FileNotFoundException(fileName);
        return await File.ReadAllBytesAsync(path, ct);
    }

    /// <summary>Re-walks one daily file, recomputing each line's hash. Returns the first broken line, or null when intact.</summary>
    public async Task<AuditVerifyResult> VerifyAsync(string fileName, CancellationToken ct = default)
    {
        var safe = Path.GetFileName(fileName);
        var path = Path.Combine(Directory, safe);
        if (!File.Exists(path)) return new AuditVerifyResult(false, 0, "File not found.");

        string? prevHash = null;
        var lineNumber = 0;
        await foreach (var line in File.ReadLinesAsync(path, ct))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            // Reconstruct the canonical payload (everything except `hash`) in sorted-key order.
            var payload = new SortedDictionary<string, object?>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "hash") continue;
                payload[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
            var canonical = JsonSerializer.Serialize(payload);
            var expected = Sha256Hex(canonical);
            var got = root.GetProperty("hash").GetString();

            if (got != expected)
            {
                return new AuditVerifyResult(false, lineNumber, $"Line {lineNumber}: hash mismatch — file has been tampered.");
            }
            var prevInLine = root.TryGetProperty("prev_hash", out var ph) ? ph.GetString() : null;
            if (prevInLine != prevHash)
            {
                return new AuditVerifyResult(false, lineNumber, $"Line {lineNumber}: prev_hash chain broken (expected {prevHash ?? "null"}, got {prevInLine ?? "null"}).");
            }
            prevHash = expected;
        }
        return new AuditVerifyResult(true, lineNumber, "Chain intact.");
    }

    // ---------- helpers ----------
    private static string? ReadLastHash(string path)
    {
        if (!File.Exists(path)) return null;
        string? last = null;
        try
        {
            // O(file) read of the last non-empty line. Audit files are small (1 day worth);
            // a fancier seek-from-end can be added if files ever exceed ~50MB.
            foreach (var line in File.ReadLines(path))
            {
                if (!string.IsNullOrWhiteSpace(line)) last = line;
            }
            if (last is null) return null;
            using var doc = JsonDocument.Parse(last);
            return doc.RootElement.TryGetProperty("hash", out var h) ? h.GetString() : null;
        }
        catch { return null; }
    }

    private static string Sha256Hex(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record AuditFileInfo(string Name, long Size, DateTime LastWriteUtc, int LineCount);
public sealed record AuditVerifyResult(bool Ok, int Lines, string Message);
