using bestgen.Services;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ReportService _reportService;

    public ReportsController(ReportService reportService)
    {
        _reportService = reportService;
    }

    public IActionResult Index() => View(_reportService.GetReportCards());

    public async Task<IActionResult> Details(
        string id,
        DateTime? fromDate,
        DateTime? toDate,
        int? customerId,
        int? supplierId,
        int? accountId,
        int? warehouseId,
        int? productId)
    {
        var card = _reportService.GetReportCards().FirstOrDefault(c => c.ReportKey == id);
        if (card is null)
        {
            return NotFound();
        }

        var filters = new ReportFilters(
            FromDate: fromDate,
            ToDate: toDate,
            AccountId: accountId,
            CustomerId: customerId,
            SupplierId: supplierId,
            WarehouseId: warehouseId,
            ProductId: productId);

        var result = await _reportService.RunAsync(id, filters);

        ViewBag.Card = card;
        ViewBag.FromDate = (filters.FromDate ?? DateTime.Today.AddMonths(-1)).ToString("yyyy-MM-dd");
        ViewBag.ToDate = (filters.ToDate ?? DateTime.Today).ToString("yyyy-MM-dd");
        return View(result);
    }
}
