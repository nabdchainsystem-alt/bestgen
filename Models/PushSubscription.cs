using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Models;

[Index(nameof(Endpoint), IsUnique = true)]
public class PushSubscription
{
    public int Id { get; set; }

    /// <summary>The Identity user this subscription belongs to. May be a portal Customer too.</summary>
    [Required, StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Endpoint { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string P256dh { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Auth { get; set; } = string.Empty;

    [StringLength(120)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
