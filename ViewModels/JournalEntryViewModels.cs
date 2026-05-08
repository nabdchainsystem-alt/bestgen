using System.ComponentModel.DataAnnotations;
using bestgen.Models;

namespace bestgen.ViewModels;

public class JournalEntryFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "رقم القيد")]
    public string? EntryNumber { get; set; }

    [Required]
    [Display(Name = "تاريخ القيد")]
    public DateTime EntryDate { get; set; } = DateTime.Today;

    [Display(Name = "المصدر")]
    [StringLength(80)]
    public string? SourceModule { get; set; }

    [Display(Name = "الوصف")]
    [StringLength(600)]
    public string? Description { get; set; }

    [Display(Name = "الحالة")]
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
    [Range(1, int.MaxValue, ErrorMessage = "اختر الحساب")]
    public int AccountId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Debit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Credit { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }
}
