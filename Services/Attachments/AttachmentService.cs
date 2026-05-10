using bestgen.Data;
using bestgen.Models;
using bestgen.Services.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Attachments;

/// <summary>
/// Stores uploaded blobs under App_Data/uploads/{tenantId}/ — outside wwwroot
/// so they aren't directly addressable by URL. Downloads must go through the
/// AttachmentsController which checks auth + the tenant query filter.
/// </summary>
public class AttachmentService
{
    public const long MaxBytes = 25L * 1024 * 1024; // 25 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp",
        ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".zip", ".heic"
    };

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ITenantContext _tenant;

    public AttachmentService(ApplicationDbContext db, IWebHostEnvironment env, ITenantContext tenant)
    {
        _db = db;
        _env = env;
        _tenant = tenant;
    }

    public async Task<(bool Success, string? Error, Attachment? Saved)> UploadAsync(
        AttachmentDocumentType type, int docId, IFormFile file,
        string? userId, string? userName, string? description, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return (false, "No file provided.", null);
        }
        if (file.Length > MaxBytes)
        {
            return (false, $"File exceeds {MaxBytes / (1024 * 1024)} MB limit.", null);
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return (false, $"Extension {ext} is not allowed.", null);
        }

        // Magic-byte check — stops users renaming evil.exe → file.pdf.
        await using (var sigStream = file.OpenReadStream())
        {
            if (!FileSignatureValidator.Validate(sigStream, ext))
            {
                return (false, $"File contents don't match the {ext} extension.", null);
            }
        }

        var safeName = Path.GetFileName(file.FileName); // strip directory traversal
        var tenantDir = Path.Combine(_env.ContentRootPath, "App_Data", "uploads", _tenant.TenantId.ToString());
        Directory.CreateDirectory(tenantDir);

        // Storage filename: {guid}{ext}. Original name kept on the entity row.
        var storageName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(tenantDir, storageName);

        try
        {
            await using (var fs = File.Create(fullPath))
            {
                await file.CopyToAsync(fs, ct);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to write file: {ex.Message}", null);
        }

        var att = new Attachment
        {
            DocumentType = type,
            DocumentId = docId,
            FileName = safeName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            StoragePath = $"{_tenant.TenantId}/{storageName}",
            UploadedByUserId = userId,
            UploadedByName = userName,
            UploadedAt = DateTime.UtcNow,
            Description = description
        };
        _db.Attachments.Add(att);
        await _db.SaveChangesAsync(ct);
        return (true, null, att);
    }

    public Task<List<Attachment>> ListAsync(AttachmentDocumentType type, int docId, CancellationToken ct = default)
    {
        return _db.Attachments.AsNoTracking()
            .Where(a => a.DocumentType == type && a.DocumentId == docId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(ct);
    }

    public async Task<(byte[]? Bytes, string? FileName, string? ContentType)> ReadAsync(int id, CancellationToken ct = default)
    {
        var a = await _db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return (null, null, null);
        var fullPath = Path.Combine(_env.ContentRootPath, "App_Data", "uploads", a.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath)) return (null, null, null);
        var bytes = await File.ReadAllBytesAsync(fullPath, ct);
        return (bytes, a.FileName, a.ContentType ?? "application/octet-stream");
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var a = await _db.Attachments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return false;
        var fullPath = Path.Combine(_env.ContentRootPath, "App_Data", "uploads", a.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { /* swallow — DB row is the source of truth */ }
        _db.Attachments.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
