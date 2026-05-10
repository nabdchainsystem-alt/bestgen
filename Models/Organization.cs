using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Models;

/// <summary>
/// A parent group above one or more <see cref="Tenant"/> workspaces.
/// Holding companies use this for consolidated reporting across subsidiaries.
/// Single-workspace customers can ignore it — every Tenant.OrganizationId is
/// nullable.
/// </summary>
[Index(nameof(Slug), IsUnique = true)]
public class Organization
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(160)]
    public string? OwnerEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
