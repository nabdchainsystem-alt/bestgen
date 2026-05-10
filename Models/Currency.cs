using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public class Currency
{
    public int Id { get; set; }

    [Required, StringLength(3)]
    public string Code { get; set; } = string.Empty; // ISO 4217 — SAR, USD, EUR, ...

    [Required, StringLength(80)]
    public string NameAr { get; set; } = string.Empty;
    [StringLength(80)]
    public string? NameEn { get; set; }

    [StringLength(8)]
    public string? Symbol { get; set; } // ر.س, $, €, ...

    public bool IsBase { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FxRate
{
    public int Id { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow.Date;

    [Required, StringLength(3)]
    public string FromCurrencyCode { get; set; } = string.Empty;

    [Required, StringLength(3)]
    public string ToCurrencyCode { get; set; } = string.Empty;

    public decimal Rate { get; set; } = 1m;

    [StringLength(200)]
    public string? Note { get; set; }
}
