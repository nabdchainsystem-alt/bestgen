using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Models;

[Index(nameof(Token), IsUnique = true)]
public class CustomerInvitation
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    [Required, StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(14);

    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(160)]
    public string? CreatedByUserId { get; set; }

    public Customer? Customer { get; set; }
}
