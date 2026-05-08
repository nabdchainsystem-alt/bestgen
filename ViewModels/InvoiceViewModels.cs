using System.ComponentModel.DataAnnotations;
using bestgen.Models;

namespace bestgen.ViewModels;

public class SalesInvoiceFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "رقم الفاتورة")]
    public string? InvoiceNumber { get; set; }

    [Required(ErrorMessage = "تاريخ الفاتورة مطلوب")]
    [Display(Name = "تاريخ الفاتورة")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Range(1, int.MaxValue, ErrorMessage = "اختر العميل")]
    [Display(Name = "العميل")]
    public int CustomerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "اختر المستودع")]
    [Display(Name = "المستودع")]
    public int WarehouseId { get; set; }

    [Display(Name = "طريقة الدفع")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Credit;

    [Display(Name = "الحالة")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    [Display(Name = "المبلغ المدفوع")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "ملاحظات")]
    [StringLength(600)]
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal RemainingAmount { get; set; }

    public List<SalesInvoiceItemFormViewModel> Items { get; set; } = new()
    {
        new SalesInvoiceItemFormViewModel()
    };
}

public class SalesInvoiceItemFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "اختر المنتج")]
    public int ProductId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Discount { get; set; }

    [Range(0, 100)]
    public decimal VatRate { get; set; } = 15;

    public decimal LineTotal { get; set; }
}

public class PurchaseInvoiceFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "رقم فاتورة الشراء")]
    public string? PurchaseInvoiceNumber { get; set; }

    [Display(Name = "مرجع فاتورة المورد")]
    [StringLength(80)]
    public string? SupplierInvoiceReference { get; set; }

    [Required(ErrorMessage = "تاريخ الفاتورة مطلوب")]
    [Display(Name = "تاريخ الفاتورة")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Range(1, int.MaxValue, ErrorMessage = "اختر المورد")]
    [Display(Name = "المورد")]
    public int SupplierId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "اختر المستودع")]
    [Display(Name = "المستودع")]
    public int WarehouseId { get; set; }

    [Display(Name = "طريقة الدفع")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Credit;

    [Display(Name = "الحالة")]
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;

    [Display(Name = "المبلغ المدفوع")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "ملاحظات")]
    [StringLength(600)]
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal RemainingAmount { get; set; }

    public List<PurchaseInvoiceItemFormViewModel> Items { get; set; } = new()
    {
        new PurchaseInvoiceItemFormViewModel()
    };
}

public class PurchaseInvoiceItemFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "اختر المنتج")]
    public int ProductId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Discount { get; set; }

    [Range(0, 100)]
    public decimal VatRate { get; set; } = 15;

    public decimal LineTotal { get; set; }
}
