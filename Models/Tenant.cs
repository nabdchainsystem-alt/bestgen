using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

/// <summary>
/// A workspace / company that owns business data. Every tenant-scoped entity
/// has a shadow <c>TenantId</c> column added via <see cref="bestgen.Data.ApplicationDbContext"/>.
/// Queries are auto-filtered by the current tenant, so two tenants never see
/// each other's customers/invoices/inventory/etc.
/// </summary>
public class Tenant
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Plan { get; set; } = "Starter";

    public bool IsActive { get; set; } = true;

    [StringLength(160)]
    public string? OwnerEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Optional parent group (holding company). When set, the consolidation
    /// page can roll this tenant's metrics up with its siblings under the
    /// same Organization. Null = standalone workspace.
    /// </summary>
    public int? OrganizationId { get; set; }

    public Organization? Organization { get; set; }
}
