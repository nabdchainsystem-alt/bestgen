using System.ComponentModel.DataAnnotations;

namespace bestgen.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? NameEn { get; set; }

    [StringLength(32)]
    public string? VatNumber { get; set; }

    [StringLength(32)]
    public string? CommercialRegistrationNumber { get; set; }

    [StringLength(40)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(160)]
    public string? Email { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
}

public class Supplier
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string SupplierCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? NameEn { get; set; }

    [StringLength(32)]
    public string? VatNumber { get; set; }

    [StringLength(32)]
    public string? CommercialRegistrationNumber { get; set; }

    [StringLength(40)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(160)]
    public string? Email { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    public decimal OpeningBalance { get; set; }

    [StringLength(80)]
    public string? PaymentTerms { get; set; }

    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();
}

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(64)]
    public string SKU { get; set; } = string.Empty;

    [StringLength(64)]
    public string? Barcode { get; set; }

    [Required, StringLength(160)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? NameEn { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public int? CategoryId { get; set; }

    [StringLength(32)]
    public string Unit { get; set; } = "قطعة";

    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal VatRate { get; set; } = 15;
    public decimal OpeningStock { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public int WarehouseId { get; set; }
    public bool TrackInventory { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Warehouse? Warehouse { get; set; }
    public ProductCategory? ProductCategory { get; set; }
    public ICollection<SalesInvoiceItem> SalesInvoiceItems { get; set; } = new List<SalesInvoiceItem>();
    public ICollection<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; } = new List<PurchaseInvoiceItem>();
}

public class Warehouse
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string WarehouseCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(120)]
    public string? ManagerName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
    public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();
}

public class SalesInvoice
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public int CustomerId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Credit;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    [StringLength(600)]
    public string? Notes { get; set; }

    /// <summary>ISO 4217 currency code the invoice is denominated in. Defaults to SAR.</summary>
    [StringLength(3)]
    public string CurrencyCode { get; set; } = "SAR";

    /// <summary>Multiplier from CurrencyCode to the company's base currency. SAR→SAR is 1.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}

public class SalesInvoiceItem
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }

    public SalesInvoice? SalesInvoice { get; set; }
    public Product? Product { get; set; }
}

public class PurchaseInvoice
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string PurchaseInvoiceNumber { get; set; } = string.Empty;

    [StringLength(80)]
    public string? SupplierInvoiceReference { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Credit;
    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;

    [StringLength(600)]
    public string? Notes { get; set; }

    [StringLength(3)]
    public string CurrencyCode { get; set; } = "SAR";

    public decimal ExchangeRate { get; set; } = 1m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<PurchaseInvoiceItem> Items { get; set; } = new List<PurchaseInvoiceItem>();
}

public class PurchaseInvoiceItem
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Discount { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }

    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public Product? Product { get; set; }
}

public class CashBox
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string CashBoxCode { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Branch { get; set; }

    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }

    [StringLength(8)]
    public string Currency { get; set; } = "SAR";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class BankAccount
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string BankName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string AccountName { get; set; } = string.Empty;

    [Required, StringLength(34)]
    public string IBAN { get; set; } = string.Empty;

    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }

    [StringLength(8)]
    public string Currency { get; set; } = "SAR";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class Expense
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ExpenseNumber { get; set; } = string.Empty;

    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [Required, StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string PaidFromType { get; set; } = "Cash";

    public int? CashBoxId { get; set; }
    public int? BankAccountId { get; set; }
    public decimal AmountBeforeVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    [StringLength(600)]
    public string? Notes { get; set; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public CashBox? CashBox { get; set; }
    public BankAccount? BankAccount { get; set; }
}

public class Account
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string AccountCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string AccountNameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? AccountNameEn { get; set; }

    public AccountType AccountType { get; set; }
    public int? ParentAccountId { get; set; }
    public bool IsActive { get; set; } = true;

    public Account? ParentAccount { get; set; }
    public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}

public class JournalEntry
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string EntryNumber { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; } = DateTime.Today;

    [StringLength(80)]
    public string? SourceModule { get; set; }

    [StringLength(600)]
    public string? Description { get; set; }

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}

public class JournalEntryLine
{
    public int Id { get; set; }
    public int JournalEntryId { get; set; }
    public int AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    public JournalEntry? JournalEntry { get; set; }
    public Account? Account { get; set; }
}

public class CompanySettings
{
    public int Id { get; set; }

    [Required, StringLength(180)]
    public string CompanyNameAr { get; set; } = string.Empty;

    [StringLength(180)]
    public string? CompanyNameEn { get; set; }

    [StringLength(32)]
    public string? VatNumber { get; set; }

    [StringLength(32)]
    public string? CommercialRegistrationNumber { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(80)]
    public string? City { get; set; }

    [StringLength(80)]
    public string Country { get; set; } = "Saudi Arabia";

    public decimal DefaultVatRate { get; set; } = 15;

    [StringLength(12)]
    public string InvoicePrefix { get; set; } = "INV";

    [StringLength(12)]
    public string PurchaseInvoicePrefix { get; set; } = "PINV";

    [StringLength(260)]
    public string? LogoPath { get; set; }

    [StringLength(8)]
    public string BaseCurrency { get; set; } = "SAR";

    [StringLength(16)]
    public string CurrencySymbol { get; set; } = "ر.س";

    [StringLength(600)]
    public string? InvoiceFooterAr { get; set; }

    [StringLength(600)]
    public string? InvoiceFooterEn { get; set; }

    public bool ShowInvoiceQr { get; set; } = true;
    public bool ZatcaEnabled { get; set; }

    [StringLength(60)]
    public string? ZatcaTaxpayerId { get; set; }

    public DateTime? LockedThrough { get; set; }
}
