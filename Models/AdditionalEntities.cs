using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

// =====================================================================
// Sales — quotations, receipts, refunds, credit notes, delivery notes,
//         sales pricing policies
// =====================================================================

public class SalesQuotation
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string QuotationNumber { get; set; } = string.Empty;

    public DateTime QuotationDate { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public DateTime? ValidUntil { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    [StringLength(600)]
    public string? Notes { get; set; }

    [StringLength(600)]
    public string? Terms { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<SalesQuotationItem> Items { get; set; } = new List<SalesQuotationItem>();
}

public class SalesQuotationItem
{
    public int Id { get; set; }
    public int SalesQuotationId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal VatRate { get; set; } = 15;
    public decimal LineTotal { get; set; }

    public SalesQuotation? SalesQuotation { get; set; }
    public Product? Product { get; set; }
}

public class SalesReceipt
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public int? CashBoxId { get; set; }
    public int? BankAccountId { get; set; }

    [StringLength(120)]
    public string? Reference { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Confirmed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
    public CashBox? CashBox { get; set; }
    public BankAccount? BankAccount { get; set; }
}

public class SalesRefundReceipt
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string RefundNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public int? RelatedInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;

    [StringLength(300)]
    public string? Reason { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
    public SalesInvoice? RelatedInvoice { get; set; }
}

public class CreditNote
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string CreditNoteNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public int? RelatedInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public decimal VatAmount { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    public CreditNoteStatus Status { get; set; } = CreditNoteStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
    public SalesInvoice? RelatedInvoice { get; set; }
}

public class DeliveryNote
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string DeliveryNoteNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public int? SalesInvoiceId { get; set; }
    public int WarehouseId { get; set; }

    public DeliveryNoteStatus Status { get; set; } = DeliveryNoteStatus.Draft;

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<DeliveryNoteItem> Items { get; set; } = new List<DeliveryNoteItem>();
}

public class DeliveryNoteItem
{
    public int Id { get; set; }
    public int DeliveryNoteId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public DeliveryNote? DeliveryNote { get; set; }
    public Product? Product { get; set; }
}

public class SalesPricePolicy
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string PolicyName { get; set; } = string.Empty;

    public int? CustomerId { get; set; }

    [StringLength(120)]
    public string? ProductCategory { get; set; }

    public decimal DiscountPercentage { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
}

// =====================================================================
// Purchases — orders, supplier payments, refunds, debit notes, goods
//             receipts, purchase pricing
// =====================================================================

public class PurchaseOrder
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public decimal Subtotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal VatRate { get; set; } = 15;
    public decimal LineTotal { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
    public Product? Product { get; set; }
}

public class SupplierPayment
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string PaymentNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public int? CashBoxId { get; set; }
    public int? BankAccountId { get; set; }

    [StringLength(120)]
    public string? Reference { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Confirmed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public CashBox? CashBox { get; set; }
    public BankAccount? BankAccount { get; set; }
}

public class PurchaseRefundReceipt
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string RefundNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public int? RelatedPurchaseInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;

    [StringLength(300)]
    public string? Reason { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public PurchaseInvoice? RelatedPurchaseInvoice { get; set; }
}

public class DebitNote
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string DebitNoteNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public int? RelatedPurchaseInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public decimal VatAmount { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    public CreditNoteStatus Status { get; set; } = CreditNoteStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public PurchaseInvoice? RelatedPurchaseInvoice { get; set; }
}

public class GoodsReceipt
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string GoodsReceiptNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public int? PurchaseInvoiceId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int WarehouseId { get; set; }

    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
}

public class GoodsReceiptItem
{
    public int Id { get; set; }
    public int GoodsReceiptId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public GoodsReceipt? GoodsReceipt { get; set; }
    public Product? Product { get; set; }
}

public class PurchasePricePolicy
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string PolicyName { get; set; } = string.Empty;

    public int? SupplierId { get; set; }
    public int? ProductId { get; set; }

    [StringLength(120)]
    public string? ProductCategory { get; set; }

    public decimal UnitCost { get; set; }
    public decimal DiscountPercentage { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public Product? Product { get; set; }
}

// =====================================================================
// Products & Inventory — categories, counts, transfers, movements
// =====================================================================

public class ProductCategory
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? NameEn { get; set; }

    [StringLength(40)]
    public string? Code { get; set; }

    public int? ParentCategoryId { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ProductCategory? ParentCategory { get; set; }
    public ICollection<ProductCategory> Children { get; set; } = new List<ProductCategory>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class InventoryCount
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string InventoryCountNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int WarehouseId { get; set; }
    public InventoryCountStatus Status { get; set; } = InventoryCountStatus.Draft;

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Warehouse? Warehouse { get; set; }
    public ICollection<InventoryCountItem> Items { get; set; } = new List<InventoryCountItem>();
}

public class InventoryCountItem
{
    public int Id { get; set; }
    public int InventoryCountId { get; set; }
    public int ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Difference { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public InventoryCount? InventoryCount { get; set; }
    public Product? Product { get; set; }
}

public class StockTransfer
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string TransferNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Warehouse? FromWarehouse { get; set; }
    public Warehouse? ToWarehouse { get; set; }
    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}

public class StockTransferItem
{
    public int Id { get; set; }
    public int StockTransferId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public StockTransfer? StockTransfer { get; set; }
    public Product? Product { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }

    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public int ProductId { get; set; }
    public int? WarehouseId { get; set; }
    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }

    [StringLength(80)]
    public string? Reference { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
}

// =====================================================================
// Fixed Assets
// =====================================================================

public class FixedAsset
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string AssetCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? NameEn { get; set; }

    [StringLength(120)]
    public string? Category { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public decimal PurchaseCost { get; set; }
    public decimal CurrentValue { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public int UsefulLifeMonths { get; set; }

    [StringLength(160)]
    public string? Location { get; set; }

    public int? ResponsibleEmployeeId { get; set; }
    public FixedAssetStatus Status { get; set; } = FixedAssetStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? ResponsibleEmployee { get; set; }
}

public class AssetTag
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string TagCode { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string TagName { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class AssetRental
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string RentalNumber { get; set; } = string.Empty;

    public int AssetId { get; set; }
    public int? CustomerId { get; set; }
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    public decimal MonthlyAmount { get; set; }

    [StringLength(40)]
    public string? Status { get; set; } = "Active";

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public FixedAsset? Asset { get; set; }
    public Customer? Customer { get; set; }
}

// =====================================================================
// Advanced accounting — general receipts, opening balances, dimensions
// =====================================================================

public class GeneralReceipt
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.Today;
    public GeneralReceiptType ReceiptType { get; set; } = GeneralReceiptType.Receipt;
    public int AccountId { get; set; }

    public int? CashBoxId { get; set; }
    public int? BankAccountId { get; set; }
    public decimal Amount { get; set; }

    [StringLength(600)]
    public string? Description { get; set; }

    [StringLength(120)]
    public string? Reference { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Confirmed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Account? Account { get; set; }
    public CashBox? CashBox { get; set; }
    public BankAccount? BankAccount { get; set; }
}

public class OpeningBalance
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public DateTime OpeningDate { get; set; } = new(DateTime.Today.Year, 1, 1);

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Account? Account { get; set; }
}

public class ReportingDimension
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string DimensionName { get; set; } = string.Empty;

    public DimensionType DimensionType { get; set; } = DimensionType.CostCenter;

    [Required, StringLength(40)]
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

// =====================================================================
// Human Resources finance
// =====================================================================

public class Employee
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string FullNameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? FullNameEn { get; set; }

    [StringLength(40)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(160)]
    public string? Email { get; set; }

    [StringLength(120)]
    public string? JobTitle { get; set; }

    [StringLength(120)]
    public string? Department { get; set; }

    public decimal Salary { get; set; }
    /// <summary>Optional housing allowance. Counted toward EOSB if BasicWage isn't set separately.</summary>
    public decimal HousingAllowance { get; set; }
    /// <summary>Optional transportation allowance.</summary>
    public decimal TransportAllowance { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>Saudi nationals are covered by full GOSI (21.75%); non-Saudi only by occupational hazards (2%).</summary>
    public bool IsSaudi { get; set; } = true;

    public decimal CurrentBalance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class EmployeeDocument
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    [Required, StringLength(120)]
    public string DocumentType { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string DocumentName { get; set; } = string.Empty;

    public DateTime? ExpiryDate { get; set; }

    [StringLength(400)]
    public string? FilePath { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class PayrollEntry
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string PayrollNumber { get; set; } = string.Empty;

    public int Month { get; set; } = DateTime.Today.Month;
    public int Year { get; set; } = DateTime.Today.Year;
    public int EmployeeId { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetSalary { get; set; }

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class EmployeeDeduction
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class EmployeeBonus
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class EmployeeLoan
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime LoanDate { get; set; } = DateTime.Today;
    public decimal LoanAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal InstallmentAmount { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class EmployeeReceipt
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public int EmployeeId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Amount { get; set; }

    [StringLength(120)]
    public string? ReceiptType { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Confirmed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class EmployeeRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    [Required, StringLength(120)]
    public string RequestType { get; set; } = string.Empty;

    public DateTime RequestDate { get; set; } = DateTime.Today;
    public EmployeeRequestStatus Status { get; set; } = EmployeeRequestStatus.Pending;

    [StringLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Employee? Employee { get; set; }
}

public class Branch
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string BranchCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? NameEn { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(40)]
    public string? Phone { get; set; }

    [StringLength(160)]
    public string? Manager { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class NumberingPolicy
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string DocumentType { get; set; } = string.Empty;

    [StringLength(60)]
    public string DisplayNameAr { get; set; } = string.Empty;

    [StringLength(60)]
    public string? DisplayNameEn { get; set; }

    [Required, StringLength(12)]
    public string Prefix { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Format { get; set; } = "{prefix}-{yyyy}-{0000}";

    public int CurrentSequence { get; set; }
    public bool ResetAnnually { get; set; } = true;
    public int LastResetYear { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class TaxRate
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(120)]
    public string? NameEn { get; set; }

    public decimal Rate { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

// =====================================================================
// Audit log — populated by the SaveChanges interceptor in Data.
// =====================================================================

public enum AuditAction
{
    Create,
    Update,
    Delete
}

public class AuditEntry
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(80)]
    public string? EntityKey { get; set; }

    public AuditAction Action { get; set; }

    [StringLength(160)]
    public string? UserName { get; set; }

    [StringLength(2000)]
    public string? Summary { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;
}
