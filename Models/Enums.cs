namespace bestgen.Models;

public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
    Cancelled
}

public enum PurchaseInvoiceStatus
{
    Draft,
    Received,
    PartiallyPaid,
    Paid,
    Cancelled
}

public enum PaymentMethod
{
    Cash,
    Bank,
    Credit
}

public enum ExpenseStatus
{
    Draft,
    Approved,
    Paid
}

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

public enum JournalEntryStatus
{
    Draft,
    Posted
}

public enum QuotationStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
    Expired
}

public enum ReceiptStatus
{
    Draft,
    Confirmed,
    Cancelled
}

public enum CreditNoteStatus
{
    Draft,
    Issued,
    Cancelled
}

public enum DeliveryNoteStatus
{
    Draft,
    Delivered,
    Cancelled
}

public enum PurchaseOrderStatus
{
    Draft,
    Sent,
    Confirmed,
    Received,
    Cancelled
}

public enum GoodsReceiptStatus
{
    Draft,
    Received,
    Cancelled
}

public enum InventoryCountStatus
{
    Draft,
    Counted,
    Approved,
    Cancelled
}

public enum StockTransferStatus
{
    Draft,
    Transferred,
    Cancelled
}

public enum StockMovementType
{
    Sales,
    Purchase,
    Transfer,
    Adjustment,
    Opening,
    Return
}

public enum FixedAssetStatus
{
    Active,
    UnderMaintenance,
    Sold,
    Retired
}

public enum DepreciationMethod
{
    StraightLine,
    DecliningBalance,
    UnitsOfProduction,
    None
}

public enum EmployeeStatus
{
    Active,
    OnLeave,
    Suspended,
    Terminated
}

public enum PayrollStatus
{
    Draft,
    Approved,
    Paid
}

public enum LoanStatus
{
    Active,
    Settled,
    Cancelled
}

public enum EmployeeRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public enum GeneralReceiptType
{
    Receipt,
    Payment
}

public enum DimensionType
{
    CostCenter,
    Branch,
    Project,
    Department
}
