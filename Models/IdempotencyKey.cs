using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Models;

/// <summary>
/// Records a client-supplied <c>Idempotency-Key</c> for a (Method+Path) so a
/// retried POST doesn't double-charge / double-create. The <see cref="IdempotentAttribute"/>
/// inserts a row before running the action; a unique index makes concurrent duplicates fail
/// fast with a 409.
/// </summary>
[Index(nameof(Key), nameof(RouteKey), IsUnique = true)]
public class IdempotencyKey
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Key { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RouteKey { get; set; } = string.Empty;

    public int? ResponseStatus { get; set; }

    [StringLength(2000)]
    public string? ResponseBody { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
}
