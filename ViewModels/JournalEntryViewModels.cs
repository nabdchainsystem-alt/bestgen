using System.ComponentModel.DataAnnotations;
using bestgen.Models;

namespace bestgen.ViewModels;

public class JournalEntryFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Entry number")]
    public string? EntryNumber { get; set; }

    [Required]
    [Display(Name = "Entry date")]
    public DateTime EntryDate { get; set; } = DateTime.Today;

    [Display(Name = "Source")]
    [StringLength(80)]
    public string? SourceModule { get; set; }

    [Display(Name = "Description")]
    [StringLength(600)]
    public string? Description { get; set; }

    [Display(Name = "Status")]
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    public List<JournalEntryLineFormViewModel> Lines { get; set; } = new()
    {
        new JournalEntryLineFormViewModel(),
        new JournalEntryLineFormViewModel()
    };
}

public class JournalEntryLineFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an account")]
    public int AccountId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Debit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Credit { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }
}
