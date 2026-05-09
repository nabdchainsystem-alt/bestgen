using bestgen.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<SalesQuotation> SalesQuotations => Set<SalesQuotation>();
    public DbSet<SalesQuotationItem> SalesQuotationItems => Set<SalesQuotationItem>();
    public DbSet<SalesReceipt> SalesReceipts => Set<SalesReceipt>();
    public DbSet<SalesRefundReceipt> SalesRefundReceipts => Set<SalesRefundReceipt>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<DeliveryNoteItem> DeliveryNoteItems => Set<DeliveryNoteItem>();
    public DbSet<SalesPricePolicy> SalesPricePolicies => Set<SalesPricePolicy>();

    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<PurchaseRefundReceipt> PurchaseRefundReceipts => Set<PurchaseRefundReceipt>();
    public DbSet<DebitNote> DebitNotes => Set<DebitNote>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();
    public DbSet<PurchasePricePolicy> PurchasePricePolicies => Set<PurchasePricePolicy>();

    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<InventoryCountItem> InventoryCountItems => Set<InventoryCountItem>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<AssetTag> AssetTags => Set<AssetTag>();
    public DbSet<AssetRental> AssetRentals => Set<AssetRental>();

    public DbSet<CashBox> CashBoxes => Set<CashBox>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<GeneralReceipt> GeneralReceipts => Set<GeneralReceipt>();
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();
    public DbSet<ReportingDimension> ReportingDimensions => Set<ReportingDimension>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<EmployeeDeduction> EmployeeDeductions => Set<EmployeeDeduction>();
    public DbSet<EmployeeBonus> EmployeeBonuses => Set<EmployeeBonus>();
    public DbSet<EmployeeLoan> EmployeeLoans => Set<EmployeeLoan>();
    public DbSet<EmployeeReceipt> EmployeeReceipts => Set<EmployeeReceipt>();
    public DbSet<EmployeeRequest> EmployeeRequests => Set<EmployeeRequest>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<NumberingPolicy> NumberingPolicies => Set<NumberingPolicy>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<decimal?>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique codes / numbers
        modelBuilder.Entity<Customer>().HasIndex(x => x.CustomerCode).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(x => x.SupplierCode).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.SKU).IsUnique();
        modelBuilder.Entity<Warehouse>().HasIndex(x => x.WarehouseCode).IsUnique();
        modelBuilder.Entity<SalesInvoice>().HasIndex(x => x.InvoiceNumber).IsUnique();
        modelBuilder.Entity<PurchaseInvoice>().HasIndex(x => x.PurchaseInvoiceNumber).IsUnique();
        modelBuilder.Entity<CashBox>().HasIndex(x => x.CashBoxCode).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(x => x.AccountCode).IsUnique();

        modelBuilder.Entity<ProductCategory>().HasIndex(x => x.Code);
        modelBuilder.Entity<SalesQuotation>().HasIndex(x => x.QuotationNumber).IsUnique();
        modelBuilder.Entity<SalesReceipt>().HasIndex(x => x.ReceiptNumber).IsUnique();
        modelBuilder.Entity<SalesRefundReceipt>().HasIndex(x => x.RefundNumber).IsUnique();
        modelBuilder.Entity<CreditNote>().HasIndex(x => x.CreditNoteNumber).IsUnique();
        modelBuilder.Entity<DeliveryNote>().HasIndex(x => x.DeliveryNoteNumber).IsUnique();
        modelBuilder.Entity<PurchaseOrder>().HasIndex(x => x.PurchaseOrderNumber).IsUnique();
        modelBuilder.Entity<SupplierPayment>().HasIndex(x => x.PaymentNumber).IsUnique();
        modelBuilder.Entity<PurchaseRefundReceipt>().HasIndex(x => x.RefundNumber).IsUnique();
        modelBuilder.Entity<DebitNote>().HasIndex(x => x.DebitNoteNumber).IsUnique();
        modelBuilder.Entity<GoodsReceipt>().HasIndex(x => x.GoodsReceiptNumber).IsUnique();
        modelBuilder.Entity<InventoryCount>().HasIndex(x => x.InventoryCountNumber).IsUnique();
        modelBuilder.Entity<StockTransfer>().HasIndex(x => x.TransferNumber).IsUnique();
        modelBuilder.Entity<FixedAsset>().HasIndex(x => x.AssetCode).IsUnique();
        modelBuilder.Entity<AssetTag>().HasIndex(x => x.TagCode).IsUnique();
        modelBuilder.Entity<AssetRental>().HasIndex(x => x.RentalNumber).IsUnique();
        modelBuilder.Entity<GeneralReceipt>().HasIndex(x => x.ReceiptNumber).IsUnique();
        modelBuilder.Entity<ReportingDimension>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(x => x.EmployeeCode).IsUnique();
        modelBuilder.Entity<PayrollEntry>().HasIndex(x => x.PayrollNumber).IsUnique();
        modelBuilder.Entity<EmployeeReceipt>().HasIndex(x => x.ReceiptNumber).IsUnique();
        modelBuilder.Entity<Branch>().HasIndex(x => x.BranchCode).IsUnique();
        modelBuilder.Entity<NumberingPolicy>().HasIndex(x => x.DocumentType).IsUnique();

        // Existing relationships
        modelBuilder.Entity<SalesInvoice>()
            .HasOne(x => x.Customer)
            .WithMany(x => x.SalesInvoices)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesInvoice>()
            .HasOne(x => x.Warehouse)
            .WithMany(x => x.SalesInvoices)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesInvoiceItem>()
            .HasOne(x => x.SalesInvoice)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SalesInvoiceItem>()
            .HasOne(x => x.Product)
            .WithMany(x => x.SalesInvoiceItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseInvoice>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.PurchaseInvoices)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseInvoice>()
            .HasOne(x => x.Warehouse)
            .WithMany(x => x.PurchaseInvoices)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseInvoiceItem>()
            .HasOne(x => x.PurchaseInvoice)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PurchaseInvoiceItem>()
            .HasOne(x => x.Product)
            .WithMany(x => x.PurchaseInvoiceItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(x => x.Warehouse)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Product>()
            .HasOne(x => x.ProductCategory)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProductCategory>()
            .HasOne(x => x.ParentCategory)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Expense>()
            .HasOne(x => x.CashBox)
            .WithMany()
            .HasForeignKey(x => x.CashBoxId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Expense>()
            .HasOne(x => x.BankAccount)
            .WithMany()
            .HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Account>()
            .HasOne(x => x.ParentAccount)
            .WithMany(x => x.ChildAccounts)
            .HasForeignKey(x => x.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JournalEntryLine>()
            .HasOne(x => x.JournalEntry)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JournalEntryLine>()
            .HasOne(x => x.Account)
            .WithMany(x => x.JournalEntryLines)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sales auxiliaries
        modelBuilder.Entity<SalesQuotation>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SalesQuotationItem>()
            .HasOne(x => x.SalesQuotation).WithMany(x => x.Items)
            .HasForeignKey(x => x.SalesQuotationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SalesQuotationItem>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesReceipt>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SalesReceipt>()
            .HasOne(x => x.CashBox).WithMany().HasForeignKey(x => x.CashBoxId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SalesReceipt>()
            .HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesRefundReceipt>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SalesRefundReceipt>()
            .HasOne(x => x.RelatedInvoice).WithMany().HasForeignKey(x => x.RelatedInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CreditNote>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CreditNote>()
            .HasOne(x => x.RelatedInvoice).WithMany().HasForeignKey(x => x.RelatedInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DeliveryNote>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeliveryNote>()
            .HasOne(x => x.SalesInvoice).WithMany().HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeliveryNote>()
            .HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeliveryNoteItem>()
            .HasOne(x => x.DeliveryNote).WithMany(x => x.Items)
            .HasForeignKey(x => x.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DeliveryNoteItem>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesPricePolicy>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Purchases auxiliaries
        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PurchaseOrderItem>()
            .HasOne(x => x.PurchaseOrder).WithMany(x => x.Items)
            .HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PurchaseOrderItem>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupplierPayment>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SupplierPayment>()
            .HasOne(x => x.CashBox).WithMany().HasForeignKey(x => x.CashBoxId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SupplierPayment>()
            .HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseRefundReceipt>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PurchaseRefundReceipt>()
            .HasOne(x => x.RelatedPurchaseInvoice).WithMany()
            .HasForeignKey(x => x.RelatedPurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DebitNote>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DebitNote>()
            .HasOne(x => x.RelatedPurchaseInvoice).WithMany()
            .HasForeignKey(x => x.RelatedPurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GoodsReceipt>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GoodsReceipt>()
            .HasOne(x => x.PurchaseInvoice).WithMany()
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GoodsReceipt>()
            .HasOne(x => x.PurchaseOrder).WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GoodsReceipt>()
            .HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GoodsReceiptItem>()
            .HasOne(x => x.GoodsReceipt).WithMany(x => x.Items)
            .HasForeignKey(x => x.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GoodsReceiptItem>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchasePricePolicy>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PurchasePricePolicy>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Inventory
        modelBuilder.Entity<InventoryCount>()
            .HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InventoryCountItem>()
            .HasOne(x => x.InventoryCount).WithMany(x => x.Items)
            .HasForeignKey(x => x.InventoryCountId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InventoryCountItem>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.FromWarehouse).WithMany().HasForeignKey(x => x.FromWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockTransferItem>()
            .HasOne(x => x.StockTransfer).WithMany(x => x.Items)
            .HasForeignKey(x => x.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<StockTransferItem>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockMovement>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockMovement>()
            .HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Fixed assets
        modelBuilder.Entity<FixedAsset>()
            .HasOne(x => x.ResponsibleEmployee).WithMany()
            .HasForeignKey(x => x.ResponsibleEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AssetRental>()
            .HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AssetRental>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Accounting
        modelBuilder.Entity<GeneralReceipt>()
            .HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GeneralReceipt>()
            .HasOne(x => x.CashBox).WithMany().HasForeignKey(x => x.CashBoxId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GeneralReceipt>()
            .HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OpeningBalance>()
            .HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // HR
        modelBuilder.Entity<EmployeeDocument>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PayrollEntry>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmployeeDeduction>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmployeeBonus>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmployeeLoan>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmployeeReceipt>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmployeeRequest>()
            .HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // String enums (already existing + new ones)
        modelBuilder.Entity<SalesInvoice>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<SalesInvoice>().Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<PurchaseInvoice>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<PurchaseInvoice>().Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<Expense>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<Account>().Property(x => x.AccountType).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<JournalEntry>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        modelBuilder.Entity<SalesQuotation>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<SalesReceipt>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<SalesReceipt>().Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<SalesRefundReceipt>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<SalesRefundReceipt>().Property(x => x.RefundMethod).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<CreditNote>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<DeliveryNote>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        modelBuilder.Entity<PurchaseOrder>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<SupplierPayment>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<SupplierPayment>().Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<PurchaseRefundReceipt>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<PurchaseRefundReceipt>().Property(x => x.RefundMethod).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<DebitNote>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<GoodsReceipt>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        modelBuilder.Entity<InventoryCount>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<StockTransfer>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<StockMovement>().Property(x => x.MovementType).HasConversion<string>().HasMaxLength(32);

        modelBuilder.Entity<FixedAsset>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<FixedAsset>().Property(x => x.DepreciationMethod).HasConversion<string>().HasMaxLength(32);

        modelBuilder.Entity<GeneralReceipt>().Property(x => x.ReceiptType).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<GeneralReceipt>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<ReportingDimension>().Property(x => x.DimensionType).HasConversion<string>().HasMaxLength(32);

        modelBuilder.Entity<Employee>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<PayrollEntry>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<EmployeeDeduction>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<EmployeeBonus>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<EmployeeLoan>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<EmployeeReceipt>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<EmployeeRequest>().Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
    }
}
