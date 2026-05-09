using System.ComponentModel.DataAnnotations;
using bestgen.Models;

namespace bestgen.ViewModels;

public class SalesInvoiceFormViewModel
{
    public int? Id { get; set; }

    [Display(Name = "Invoice number")]
    public string? InvoiceNumber { get; set; }

    [Required(ErrorMessage = "Invoice date is required")]
    [Display(Name = "Invoice date")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a customer")]
    [Display(Name = "Customer")]
    public int CustomerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a warehouse")]
    [Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Credit;

    [Display(Name = "Status")]
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    [Display(Name = "Paid amount")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "Notes")]
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
    [Range(1, int.MaxValue, ErrorMessage = "Please select a product")]
    public int ProductId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
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

    [Display(Name = "Invoice number")]
    public string? PurchaseInvoiceNumber { get; set; }

    [Display(Name = "Supplier invoice reference")]
    [StringLength(80)]
    public string? SupplierInvoiceReference { get; set; }

    [Required(ErrorMessage = "Invoice date is required")]
    [Display(Name = "Invoice date")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a supplier")]
    [Display(Name = "Supplier")]
    public int SupplierId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a warehouse")]
    [Display(Name = "Warehouse")]
    public int WarehouseId { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Credit;

    [Display(Name = "Status")]
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;

    [Display(Name = "Paid amount")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "Notes")]
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
    [Range(1, int.MaxValue, ErrorMessage = "Please select a product")]
    public int ProductId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than zero")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Discount { get; set; }

    [Range(0, 100)]
    public decimal VatRate { get; set; } = 15;

    public decimal LineTotal { get; set; }
}
