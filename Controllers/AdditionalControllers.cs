using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

// Sales auxiliaries

public class SalesQuotationsController : CrudController<SalesQuotation>
{
    public SalesQuotationsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<SalesQuotation> Query() => Context.SalesQuotations
        .AsNoTracking()
        .Include(x => x.Customer);
}

public class SalesReceiptsController : CrudController<SalesReceipt>
{
    public SalesReceiptsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<SalesReceipt> Query() => Context.SalesReceipts
        .AsNoTracking()
        .Include(x => x.Customer);
}

public class SalesRefundReceiptsController : CrudController<SalesRefundReceipt>
{
    public SalesRefundReceiptsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<SalesRefundReceipt> Query() => Context.SalesRefundReceipts
        .AsNoTracking()
        .Include(x => x.Customer);
}

public class CreditNotesController : CrudController<CreditNote>
{
    public CreditNotesController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<CreditNote> Query() => Context.CreditNotes
        .AsNoTracking()
        .Include(x => x.Customer);
}

public class DeliveryNotesController : CrudController<DeliveryNote>
{
    public DeliveryNotesController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<DeliveryNote> Query() => Context.DeliveryNotes
        .AsNoTracking()
        .Include(x => x.Customer)
        .Include(x => x.Warehouse);
}

public class SalesPricePoliciesController : CrudController<SalesPricePolicy>
{
    public SalesPricePoliciesController(ApplicationDbContext context) : base(context)
    {
    }
}

// Purchases auxiliaries

public class PurchaseOrdersController : CrudController<PurchaseOrder>
{
    public PurchaseOrdersController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<PurchaseOrder> Query() => Context.PurchaseOrders
        .AsNoTracking()
        .Include(x => x.Supplier);
}

public class SupplierPaymentsController : CrudController<SupplierPayment>
{
    public SupplierPaymentsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<SupplierPayment> Query() => Context.SupplierPayments
        .AsNoTracking()
        .Include(x => x.Supplier);
}

public class PurchaseRefundReceiptsController : CrudController<PurchaseRefundReceipt>
{
    public PurchaseRefundReceiptsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<PurchaseRefundReceipt> Query() => Context.PurchaseRefundReceipts
        .AsNoTracking()
        .Include(x => x.Supplier);
}

public class DebitNotesController : CrudController<DebitNote>
{
    public DebitNotesController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<DebitNote> Query() => Context.DebitNotes
        .AsNoTracking()
        .Include(x => x.Supplier);
}

public class GoodsReceiptsController : CrudController<GoodsReceipt>
{
    public GoodsReceiptsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<GoodsReceipt> Query() => Context.GoodsReceipts
        .AsNoTracking()
        .Include(x => x.Supplier)
        .Include(x => x.Warehouse);
}

public class PurchasePricePoliciesController : CrudController<PurchasePricePolicy>
{
    public PurchasePricePoliciesController(ApplicationDbContext context) : base(context)
    {
    }
}

// Inventory auxiliaries

public class ProductCategoriesController : CrudController<ProductCategory>
{
    public ProductCategoriesController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<ProductCategory> Query() => Context.ProductCategories
        .AsNoTracking()
        .Include(x => x.ParentCategory);
}

public class InventoryCountsController : CrudController<InventoryCount>
{
    public InventoryCountsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<InventoryCount> Query() => Context.InventoryCounts
        .AsNoTracking()
        .Include(x => x.Warehouse);
}

public class StockTransfersController : CrudController<StockTransfer>
{
    public StockTransfersController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<StockTransfer> Query() => Context.StockTransfers
        .AsNoTracking()
        .Include(x => x.FromWarehouse)
        .Include(x => x.ToWarehouse);
}

// Fixed assets

public class FixedAssetsController : CrudController<FixedAsset>
{
    public FixedAssetsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<FixedAsset> Query() => Context.FixedAssets
        .AsNoTracking()
        .Include(x => x.ResponsibleEmployee);
}

public class AssetTagsController : CrudController<AssetTag>
{
    public AssetTagsController(ApplicationDbContext context) : base(context)
    {
    }
}

public class AssetRentalsController : CrudController<AssetRental>
{
    public AssetRentalsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<AssetRental> Query() => Context.AssetRentals
        .AsNoTracking()
        .Include(x => x.Asset)
        .Include(x => x.Customer);
}

// Advanced accounting

public class GeneralReceiptsController : CrudController<GeneralReceipt>
{
    public GeneralReceiptsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<GeneralReceipt> Query() => Context.GeneralReceipts
        .AsNoTracking()
        .Include(x => x.Account);
}

public class OpeningBalancesController : CrudController<OpeningBalance>
{
    public OpeningBalancesController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<OpeningBalance> Query() => Context.OpeningBalances
        .AsNoTracking()
        .Include(x => x.Account);
}

public class ReportingDimensionsController : CrudController<ReportingDimension>
{
    public ReportingDimensionsController(ApplicationDbContext context) : base(context)
    {
    }
}

// HR finance

public class EmployeesController : CrudController<Employee>
{
    public EmployeesController(ApplicationDbContext context) : base(context)
    {
    }
}

public class EmployeeDocumentsController : CrudController<EmployeeDocument>
{
    public EmployeeDocumentsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<EmployeeDocument> Query() => Context.EmployeeDocuments
        .AsNoTracking()
        .Include(x => x.Employee);
}

public class PayrollEntriesController : CrudController<PayrollEntry>
{
    public PayrollEntriesController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<PayrollEntry> Query() => Context.PayrollEntries
        .AsNoTracking()
        .Include(x => x.Employee);
}

public class EmployeeDeductionsController : CrudController<EmployeeDeduction>
{
    public EmployeeDeductionsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<EmployeeDeduction> Query() => Context.EmployeeDeductions
        .AsNoTracking()
        .Include(x => x.Employee);
}

public class EmployeeBonusesController : CrudController<EmployeeBonus>
{
    public EmployeeBonusesController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<EmployeeBonus> Query() => Context.EmployeeBonuses
        .AsNoTracking()
        .Include(x => x.Employee);
}

public class EmployeeLoansController : CrudController<EmployeeLoan>
{
    public EmployeeLoansController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<EmployeeLoan> Query() => Context.EmployeeLoans
        .AsNoTracking()
        .Include(x => x.Employee);
}

public class EmployeeReceiptsController : CrudController<EmployeeReceipt>
{
    public EmployeeReceiptsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<EmployeeReceipt> Query() => Context.EmployeeReceipts
        .AsNoTracking()
        .Include(x => x.Employee);
}

public class EmployeeRequestsController : CrudController<EmployeeRequest>
{
    public EmployeeRequestsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<EmployeeRequest> Query() => Context.EmployeeRequests
        .AsNoTracking()
        .Include(x => x.Employee);
}
