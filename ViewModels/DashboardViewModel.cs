using bestgen.Models;

namespace bestgen.ViewModels;

public class DashboardViewModel
{
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal NetProfit { get; set; }
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal Receivables { get; set; }
    public decimal Payables { get; set; }
    public decimal InventoryValue { get; set; }
    public int LowStockCount { get; set; }
    public int OverdueInvoiceCount { get; set; }

    public List<MonthlyComparisonPoint> MonthlyComparison { get; set; } = new();
    public List<CategoryExpensePoint> ExpensesByCategory { get; set; } = new();
    public List<CashFlowPoint> CashFlow { get; set; } = new();
    public List<InventoryValuePoint> InventoryByValue { get; set; } = new();

    public List<SalesInvoice> RecentSalesInvoices { get; set; } = new();
    public List<PurchaseInvoice> RecentPurchaseInvoices { get; set; } = new();
    public List<Product> LowStockProducts { get; set; } = new();
    public List<Customer> TopDebtorCustomers { get; set; } = new();
    public List<Supplier> TopCreditorSuppliers { get; set; } = new();
    public List<SalesInvoice> UnpaidInvoices { get; set; } = new();

    public List<SmartAlert> Alerts { get; set; } = new();
}

public class MonthlyComparisonPoint
{
    public string Month { get; set; } = string.Empty;
    public decimal Sales { get; set; }
    public decimal Purchases { get; set; }
}

public class CategoryExpensePoint
{
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class CashFlowPoint
{
    public string Month { get; set; } = string.Empty;
    public decimal Inflow { get; set; }
    public decimal Outflow { get; set; }
}

public class InventoryValuePoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class SmartAlert
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Tone { get; set; } = "info"; // info | warning | danger | success
    public string Icon { get; set; } = "bi-info-circle";
}
