using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Models;

[Index(nameof(KeyHash), IsUnique = true)]
public class ApiKey
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the issued key (we never store the raw key).</summary>
    [Required, StringLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Last 4 chars of the issued key — shown in the dashboard so users can identify keys.</summary>
    [StringLength(4)]
    public string? LastFour { get; set; }

    /// <summary>Comma-separated scopes — e.g. "customers:read,invoices:read,invoices:write".</summary>
    [StringLength(500)]
    public string Scopes { get; set; } = "*";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    [StringLength(160)]
    public string? CreatedByUserId { get; set; }
}
