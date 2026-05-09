using bestgen.Data;
using bestgen.Models;
using bestgen.Services;
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
    private readonly SalesReceiptService _service;

    public SalesReceiptsController(ApplicationDbContext context, SalesReceiptService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<SalesReceipt> Query() => Context.SalesReceipts
        .AsNoTracking()
        .Include(x => x.Customer);

    protected override Task BeforeCreateSaveAsync(SalesReceipt entity) =>
        _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(SalesReceipt entity) =>
        _service.PostAsync(entity);

    protected override async Task AfterSaveAsync(SalesReceipt entity, bool isCreate)
    {
        if (!isCreate)
        {
            return;
        }
        await _service.ApplyAsync(entity);
        await Context.SaveChangesAsync();
    }
}

public class SalesRefundReceiptsController : CrudController<SalesRefundReceipt>
{
    private readonly SalesRefundReceiptService _service;

    public SalesRefundReceiptsController(ApplicationDbContext context, SalesRefundReceiptService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<SalesRefundReceipt> Query() => Context.SalesRefundReceipts
        .AsNoTracking()
        .Include(x => x.Customer);

    protected override Task BeforeCreateSaveAsync(SalesRefundReceipt entity) =>
        _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(SalesRefundReceipt entity) =>
        _service.PostAsync(entity);

    protected override async Task AfterSaveAsync(SalesRefundReceipt entity, bool isCreate)
    {
        if (!isCreate) return;
        await _service.ApplyAsync(entity);
        await Context.SaveChangesAsync();
    }
}

public class CreditNotesController : CrudController<CreditNote>
{
    private readonly CreditNoteService _service;

    public CreditNotesController(ApplicationDbContext context, CreditNoteService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<CreditNote> Query() => Context.CreditNotes
        .AsNoTracking()
        .Include(x => x.Customer);

    protected override Task BeforeCreateSaveAsync(CreditNote entity) =>
        _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(CreditNote entity) =>
        _service.PostAsync(entity);

    protected override async Task AfterSaveAsync(CreditNote entity, bool isCreate)
    {
        if (!isCreate) return;
        await _service.ApplyAsync(entity);
        await Context.SaveChangesAsync();
    }
}

public class DeliveryNotesController : CrudController<DeliveryNote>
{
    private readonly DeliveryNoteService _service;

    public DeliveryNotesController(ApplicationDbContext context, DeliveryNoteService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<DeliveryNote> Query() => Context.DeliveryNotes
        .AsNoTracking()
        .Include(x => x.Customer)
        .Include(x => x.Warehouse);

    protected override Task BeforeCreateSaveAsync(DeliveryNote entity) =>
        _service.PrepareAsync(entity);
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
    private readonly SupplierPaymentService _service;

    public SupplierPaymentsController(ApplicationDbContext context, SupplierPaymentService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<SupplierPayment> Query() => Context.SupplierPayments
        .AsNoTracking()
        .Include(x => x.Supplier);

    protected override Task BeforeCreateSaveAsync(SupplierPayment entity) =>
        _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(SupplierPayment entity) =>
        _service.PostAsync(entity);

    protected override async Task AfterSaveAsync(SupplierPayment entity, bool isCreate)
    {
        if (!isCreate) return;
        await _service.ApplyAsync(entity);
        await Context.SaveChangesAsync();
    }
}

public class PurchaseRefundReceiptsController : CrudController<PurchaseRefundReceipt>
{
    private readonly PurchaseRefundReceiptService _service;

    public PurchaseRefundReceiptsController(ApplicationDbContext context, PurchaseRefundReceiptService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<PurchaseRefundReceipt> Query() => Context.PurchaseRefundReceipts
        .AsNoTracking()
        .Include(x => x.Supplier);

    protected override Task BeforeCreateSaveAsync(PurchaseRefundReceipt entity) =>
        _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(PurchaseRefundReceipt entity) =>
        _service.PostAsync(entity);

    protected override async Task AfterSaveAsync(PurchaseRefundReceipt entity, bool isCreate)
    {
        if (!isCreate) return;
        await _service.ApplyAsync(entity);
        await Context.SaveChangesAsync();
    }
}

public class DebitNotesController : CrudController<DebitNote>
{
    private readonly DebitNoteService _service;

    public DebitNotesController(ApplicationDbContext context, DebitNoteService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<DebitNote> Query() => Context.DebitNotes
        .AsNoTracking()
        .Include(x => x.Supplier);

    protected override Task BeforeCreateSaveAsync(DebitNote entity) =>
        _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(DebitNote entity) =>
        _service.PostAsync(entity);

    protected override async Task AfterSaveAsync(DebitNote entity, bool isCreate)
    {
        if (!isCreate) return;
        await _service.ApplyAsync(entity);
        await Context.SaveChangesAsync();
    }
}

public class GoodsReceiptsController : CrudController<GoodsReceipt>
{
    private readonly GoodsReceiptService _service;

    public GoodsReceiptsController(ApplicationDbContext context, GoodsReceiptService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<GoodsReceipt> Query() => Context.GoodsReceipts
        .AsNoTracking()
        .Include(x => x.Supplier)
        .Include(x => x.Warehouse);

    protected override Task BeforeCreateSaveAsync(GoodsReceipt entity) =>
        _service.PrepareAsync(entity);
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
    private readonly InventoryCountService _service;

    public InventoryCountsController(ApplicationDbContext context, InventoryCountService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<InventoryCount> Query() => Context.InventoryCounts
        .AsNoTracking()
        .Include(x => x.Warehouse);

    protected override Task BeforeCreateSaveAsync(InventoryCount entity) =>
        _service.PrepareAsync(entity);

    protected override async Task AfterSaveAsync(InventoryCount entity, bool isCreate)
    {
        if (!isCreate) return;
        // Items are usually empty on the auto-CRUD flow; richer multi-line UX
        // can populate them and this path will then post the variance.
        await _service.ApplyAsync(entity);
        await Context.SaveChangesAsync();
    }
}

public class StockTransfersController : CrudController<StockTransfer>
{
    private readonly StockTransferService _service;

    public StockTransfersController(ApplicationDbContext context, StockTransferService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<StockTransfer> Query() => Context.StockTransfers
        .AsNoTracking()
        .Include(x => x.FromWarehouse)
        .Include(x => x.ToWarehouse);

    protected override Task BeforeCreateSaveAsync(StockTransfer entity) =>
        _service.PrepareAsync(entity);

    protected override async Task AfterSaveAsync(StockTransfer entity, bool isCreate)
    {
        if (!isCreate) return;
        await _service.ApplyMovementsAsync(entity);
        await Context.SaveChangesAsync();
    }
}

// Fixed assets

public class FixedAssetsController : CrudController<FixedAsset>
{
    private readonly FixedAssetService _service;

    public FixedAssetsController(ApplicationDbContext context, FixedAssetService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<FixedAsset> Query() => Context.FixedAssets
        .AsNoTracking()
        .Include(x => x.ResponsibleEmployee);

    protected override Task AfterAddBeforeSaveAsync(FixedAsset entity) => _service.PostAcquisitionAsync(entity);
}

public class AssetTagsController : CrudController<AssetTag>
{
    public AssetTagsController(ApplicationDbContext context) : base(context)
    {
    }
}

public class AssetRentalsController : CrudController<AssetRental>
{
    private readonly AssetRentalService _service;

    public AssetRentalsController(ApplicationDbContext context, AssetRentalService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<AssetRental> Query() => Context.AssetRentals
        .AsNoTracking()
        .Include(x => x.Asset)
        .Include(x => x.Customer);

    protected override Task BeforeCreateSaveAsync(AssetRental entity) => _service.PrepareAsync(entity);
}

// Advanced accounting

public class GeneralReceiptsController : CrudController<GeneralReceipt>
{
    private readonly GeneralReceiptService _service;

    public GeneralReceiptsController(ApplicationDbContext context, GeneralReceiptService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<GeneralReceipt> Query() => Context.GeneralReceipts
        .AsNoTracking()
        .Include(x => x.Account);

    protected override Task BeforeCreateSaveAsync(GeneralReceipt entity) =>
        _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(GeneralReceipt entity) =>
        _service.PostAsync(entity);
}

public class OpeningBalancesController : CrudController<OpeningBalance>
{
    private readonly OpeningBalanceService _service;

    public OpeningBalancesController(ApplicationDbContext context, OpeningBalanceService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<OpeningBalance> Query() => Context.OpeningBalances
        .AsNoTracking()
        .Include(x => x.Account);

    protected override Task AfterAddBeforeSaveAsync(OpeningBalance entity) =>
        _service.PostAsync(entity);
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
    private readonly PayrollService _service;

    public PayrollEntriesController(ApplicationDbContext context, PayrollService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<PayrollEntry> Query() => Context.PayrollEntries
        .AsNoTracking()
        .Include(x => x.Employee);

    protected override Task BeforeCreateSaveAsync(PayrollEntry entity) => _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(PayrollEntry entity) => _service.PostAsync(entity);
}

public class EmployeeDeductionsController : CrudController<EmployeeDeduction>
{
    private readonly EmployeeDeductionService _service;

    public EmployeeDeductionsController(ApplicationDbContext context, EmployeeDeductionService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<EmployeeDeduction> Query() => Context.EmployeeDeductions
        .AsNoTracking()
        .Include(x => x.Employee);

    protected override Task AfterAddBeforeSaveAsync(EmployeeDeduction entity) => _service.PostAsync(entity);
}

public class EmployeeBonusesController : CrudController<EmployeeBonus>
{
    private readonly EmployeeBonusService _service;

    public EmployeeBonusesController(ApplicationDbContext context, EmployeeBonusService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<EmployeeBonus> Query() => Context.EmployeeBonuses
        .AsNoTracking()
        .Include(x => x.Employee);

    protected override Task AfterAddBeforeSaveAsync(EmployeeBonus entity) => _service.PostAsync(entity);
}

public class EmployeeLoansController : CrudController<EmployeeLoan>
{
    private readonly EmployeeLoanService _service;

    public EmployeeLoansController(ApplicationDbContext context, EmployeeLoanService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<EmployeeLoan> Query() => Context.EmployeeLoans
        .AsNoTracking()
        .Include(x => x.Employee);

    protected override Task AfterAddBeforeSaveAsync(EmployeeLoan entity) => _service.PostAsync(entity);
}

public class EmployeeReceiptsController : CrudController<EmployeeReceipt>
{
    private readonly EmployeeReceiptService _service;

    public EmployeeReceiptsController(ApplicationDbContext context, EmployeeReceiptService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<EmployeeReceipt> Query() => Context.EmployeeReceipts
        .AsNoTracking()
        .Include(x => x.Employee);

    protected override Task BeforeCreateSaveAsync(EmployeeReceipt entity) => _service.PrepareAsync(entity);

    protected override Task AfterAddBeforeSaveAsync(EmployeeReceipt entity) => _service.PostAsync(entity);
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

public class BranchesController : CrudController<Branch>
{
    public BranchesController(ApplicationDbContext context) : base(context)
    {
    }
}

public class NumberingPoliciesController : CrudController<NumberingPolicy>
{
    public NumberingPoliciesController(ApplicationDbContext context) : base(context)
    {
    }
}

public class TaxRatesController : CrudController<TaxRate>
{
    public TaxRatesController(ApplicationDbContext context) : base(context)
    {
    }
}
