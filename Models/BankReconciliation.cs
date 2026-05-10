using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public class BankStatement
{
    public int Id { get; set; }

    public int BankAccountId { get; set; }

    [Required, StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }

    public int LineCount { get; set; }

    public BankAccount? BankAccount { get; set; }
    public ICollection<BankStatementLine> Lines { get; set; } = new List<BankStatementLine>();
}

public class BankStatementLine
{
    public int Id { get; set; }

    public int BankStatementId { get; set; }

    public DateTime Date { get; set; }

    [StringLength(400)]
    public string? Description { get; set; }

    [StringLength(80)]
    public string? Reference { get; set; }

    /// <summary>Positive for credit (money in), negative for debit (money out).</summary>
    public decimal Amount { get; set; }

    public decimal? Balance { get; set; }

    public bool IsMatched { get; set; }

    public int? MatchedJournalEntryId { get; set; }

    public DateTime? MatchedAt { get; set; }

    [StringLength(160)]
    public string? MatchedByUserName { get; set; }

    [StringLength(400)]
    public string? Notes { get; set; }

    public BankStatement? BankStatement { get; set; }
    public JournalEntry? MatchedJournalEntry { get; set; }
}
