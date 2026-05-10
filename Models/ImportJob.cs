using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public enum ImportEntity
{
    Customer,
    Supplier,
    Product
}

public enum ImportStatus
{
    Pending,
    Completed,
    Failed
}

public class ImportJob
{
    public int Id { get; set; }

    public ImportEntity Entity { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Pending;

    [Required, StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedRows { get; set; }
    public int FailedRows { get; set; }

    /// <summary>JSON: original column header → entity field name, or null when skipped.</summary>
    [StringLength(2000)]
    public string? ColumnMappingJson { get; set; }

    [StringLength(4000)]
    public string? ErrorSummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [StringLength(160)]
    public string? CreatedByUserId { get; set; }
}
