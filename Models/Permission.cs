using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Models;

[Index(nameof(Code), IsUnique = true)]
public class Permission
{
    public int Id { get; set; }

    /// <summary>Stable machine code, e.g. "invoices.delete". Used in <see cref="bestgen.Services.Authorization.RequirePermissionAttribute"/>.</summary>
    [Required, StringLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Module { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string DisplayNameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? DisplayNameEn { get; set; }

    [StringLength(400)]
    public string? Description { get; set; }
}

[Index(nameof(RoleId), nameof(PermissionCode), IsUnique = true)]
public class RolePermission
{
    public int Id { get; set; }

    [Required, StringLength(450)]
    public string RoleId { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string PermissionCode { get; set; } = string.Empty;

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}
