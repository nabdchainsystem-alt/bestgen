using System.Globalization;
using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

public class DashboardService
{
    private static readonly CultureInfo ArabicCulture = new("ar-SA");

    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventoryService;

    public DashboardService(ApplicationDbContext context, InventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
    }

    public async Task<DashboardViewModel> GetDashboardAsync()
    {
        var sales = await _context.SalesInvoices
            .AsNoTracking()
            .Where(invoice => invoice.Status != InvoiceStatus.Cancelled)
            .ToListAsync();

        var purchases = await _context.PurchaseInvoices
            .AsNoTracking()
            .Where(invoice => invoice.Status != PurchaseInvoiceStatus.Cancelled)
            .ToListAsync();

        var expenses = await _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.Status == ExpenseStatus.Paid)
            .ToListAsync();

        var totalSales = sales.Sum(invoice => invoice.GrandTotal);
        var totalPurchases = purchases.Sum(invoice => invoice.GrandTotal);
        var totalExpenses = expenses.Sum(expense => expense.TotalAmount);

        var lowStockProducts = await _inventoryService.GetLowStockProductsAsync(20);

        var overdueInvoices = sales
            .Where(invoice => invoice.RemainingAmount > 0
                              && invoice.Status != InvoiceStatus.Cancelled
                              && invoice.InvoiceDate < DateTime.Today.AddDays(-30))
            .ToList();

        // SQLite cannot SUM decimals — materialize then sum client-side.
        var cashBalance = (await _context.CashBoxes.AsNoTracking().Select(x => x.CurrentBalance).ToListAsync()).Sum();
        var bankBalance = (await _context.BankAccounts.AsNoTracking().Select(x => x.CurrentBalance).ToListAsync()).Sum();
        var inventoryValue = (await _context.Products.AsNoTracking()
            .Select(p => new { p.CurrentStock, p.PurchasePrice })
            .ToListAsync()).Sum(row => row.CurrentStock * row.PurchasePrice);

        var model = new DashboardViewModel
        {
            TotalSales = totalSales,
            TotalPurchases = totalPurchases,
            NetProfit = totalSales - totalPurchases - totalExpenses,
            CashBalance = cashBalance,
            BankBalance = bankBalance,
            Receivables = sales.Sum(invoice => invoice.RemainingAmount),
            Payables = purchases.Sum(invoice => invoice.RemainingAmount),
            InventoryValue = inventoryValue,
            LowStockCount = lowStockProducts.Count,
            OverdueInvoiceCount = overdueInvoices.Count,
            LowStockProducts = lowStockProducts.Take(6).ToList(),
            UnpaidInvoices = (await _context.SalesInvoices.AsNoTracking().Include(x => x.Customer)
                    .Where(invoice => invoice.RemainingAmount > 0 && invoice.Status != InvoiceStatus.Cancelled)
                    .ToListAsync())
                .OrderByDescending(invoice => invoice.RemainingAmount)
                .Take(5)
                .ToList(),
            RecentSalesInvoices = await _context.SalesInvoices.AsNoTracking().Include(x => x.Customer).OrderByDescending(x => x.InvoiceDate).Take(6).ToListAsync(),
            RecentPurchaseInvoices = await _context.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).OrderByDescending(x => x.InvoiceDate).Take(6).ToListAsync()
        };

        // Customers / suppliers ranking — by computed open balances
        var customerOutstanding = sales
            .GroupBy(invoice => invoice.CustomerId)
            .Select(group => new { CustomerId = group.Key, Outstanding = group.Sum(x => x.RemainingAmount) })
            .Where(group => group.Outstanding > 0)
            .OrderByDescending(group => group.Outstanding)
            .Take(5)
            .ToList();

        if (customerOutstanding.Count > 0)
        {
            var ids = customerOutstanding.Select(x => x.CustomerId).ToList();
            var customers = await _context.Customers
                .AsNoTracking()
                .Where(customer => ids.Contains(customer.Id))
                .ToListAsync();
            model.TopDebtorCustomers = customerOutstanding
                .Select(entry =>
                {
                    var customer = customers.FirstOrDefault(c => c.Id == entry.CustomerId);
                    if (customer is null)
                    {
                        return null!;
                    }
                    customer.CurrentBalance = entry.Outstanding;
                    return customer;
                })
                .Where(customer => customer is not null)
                .ToList();
        }

        var supplierOutstanding = purchases
            .GroupBy(invoice => invoice.SupplierId)
            .Select(group => new { SupplierId = group.Key, Outstanding = group.Sum(x => x.RemainingAmount) })
            .Where(group => group.Outstanding > 0)
            .OrderByDescending(group => group.Outstanding)
            .Take(5)
            .ToList();

        if (supplierOutstanding.Count > 0)
        {
            var ids = supplierOutstanding.Select(x => x.SupplierId).ToList();
            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .Where(supplier => ids.Contains(supplier.Id))
                .ToListAsync();
            model.TopCreditorSuppliers = supplierOutstanding
                .Select(entry =>
                {
                    var supplier = suppliers.FirstOrDefault(s => s.Id == entry.SupplierId);
                    if (supplier is null)
                    {
                        return null!;
                    }
                    supplier.CurrentBalance = entry.Outstanding;
                    return supplier;
                })
                .Where(supplier => supplier is not null)
                .ToList();
        }

        // 6-month rolling sales/purchases + cash flow
        var startMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        for (var i = 0; i < 6; i++)
        {
            var month = startMonth.AddMonths(i);
            var label = month.ToString("MMM yyyy", ArabicCulture);
            var inSales = sales.Where(invoice => invoice.InvoiceDate.Year == month.Year && invoice.InvoiceDate.Month == month.Month).Sum(x => x.GrandTotal);
            var inPurchases = purchases.Where(invoice => invoice.InvoiceDate.Year == month.Year && invoice.InvoiceDate.Month == month.Month).Sum(x => x.GrandTotal);
            var inExpenses = expenses.Where(expense => expense.ExpenseDate.Year == month.Year && expense.ExpenseDate.Month == month.Month).Sum(x => x.TotalAmount);

            model.MonthlyComparison.Add(new MonthlyComparisonPoint
            {
                Month = label,
                Sales = inSales,
                Purchases = inPurchases
            });

            model.CashFlow.Add(new CashFlowPoint
            {
                Month = label,
                Inflow = inSales,
                Outflow = inPurchases + inExpenses
            });
        }

        model.ExpensesByCategory = expenses
            .GroupBy(expense => expense.Category)
            .Select(group => new CategoryExpensePoint { Category = group.Key, Total = group.Sum(expense => expense.TotalAmount) })
            .OrderByDescending(point => point.Total)
            .Take(6)
            .ToList();

        // Inventory grouped by category (string fallback) — sort client-side because
        // SQLite EF cannot ORDER BY a decimal aggregate.
        var inventoryRows = await _context.Products
            .AsNoTracking()
            .Select(product => new
            {
                Category = product.Category ?? "غير مصنف",
                Value = product.CurrentStock * product.PurchasePrice
            })
            .ToListAsync();

        model.InventoryByValue = inventoryRows
            .GroupBy(row => row.Category)
            .Select(group => new InventoryValuePoint
            {
                Label = group.Key,
                Value = group.Sum(row => row.Value)
            })
            .OrderByDescending(point => point.Value)
            .Take(8)
            .ToList();

        // Smart alerts
        if (lowStockProducts.Count > 0)
        {
            var sample = lowStockProducts.First();
            model.Alerts.Add(new SmartAlert
            {
                Tone = "warning",
                Icon = "bi-exclamation-triangle",
                Title = "تنبيه مخزون منخفض",
                Message = $"{lowStockProducts.Count} منتج وصل إلى حد الطلب — مثال: {sample.NameAr}."
            });
        }

        if (overdueInvoices.Count > 0)
        {
            model.Alerts.Add(new SmartAlert
            {
                Tone = "danger",
                Icon = "bi-clock-history",
                Title = "فواتير عملاء متأخرة",
                Message = $"{overdueInvoices.Count} فاتورة عميل متأخرة عن السداد لأكثر من 30 يوما."
            });
        }

        if (model.Payables > 0)
        {
            model.Alerts.Add(new SmartAlert
            {
                Tone = "info",
                Icon = "bi-calendar2-event",
                Title = "دفعات موردين مستحقة",
                Message = $"إجمالي مستحقات الموردين الحالية {model.Payables.ToString("N2", ArabicCulture)} ر.س."
            });
        }

        if (model.CashBalance < 5000)
        {
            model.Alerts.Add(new SmartAlert
            {
                Tone = "warning",
                Icon = "bi-cash",
                Title = "انخفاض رصيد الخزينة",
                Message = "رصيد الصناديق الإجمالي قارب على الحد الأدنى — يفضل المراجعة."
            });
        }

        var stagnantCandidates = await _context.Products
            .AsNoTracking()
            .Where(product => product.CurrentStock > product.MinimumStockLevel * 4 && product.MinimumStockLevel > 0)
            .ToListAsync();
        var stagnant = stagnantCandidates
            .OrderByDescending(product => product.CurrentStock)
            .Take(1)
            .ToList();

        if (stagnant.Count > 0)
        {
            model.Alerts.Add(new SmartAlert
            {
                Tone = "info",
                Icon = "bi-archive",
                Title = "منتج راكد",
                Message = $"{stagnant[0].NameAr} لديه مخزون يفوق ضعفي حد الطلب بشكل ملحوظ."
            });
        }

        var unusualExpense = expenses
            .GroupBy(expense => expense.Category)
            .Select(group => new { group.Key, Total = group.Sum(x => x.TotalAmount) })
            .OrderByDescending(group => group.Total)
            .FirstOrDefault();
        if (unusualExpense is not null && unusualExpense.Total > 5000)
        {
            model.Alerts.Add(new SmartAlert
            {
                Tone = "warning",
                Icon = "bi-receipt",
                Title = "مصروف غير معتاد",
                Message = $"إجمالي مصروفات «{unusualExpense.Key}» مرتفع نسبيا في الفترة الأخيرة."
            });
        }

        if (model.Alerts.Count == 0)
        {
            model.Alerts.Add(new SmartAlert
            {
                Tone = "success",
                Icon = "bi-check2-circle",
                Title = "كل شيء يعمل بسلاسة",
                Message = "لا توجد تنبيهات حرجة في الوقت الحالي."
            });
        }

        return model;
    }
}
