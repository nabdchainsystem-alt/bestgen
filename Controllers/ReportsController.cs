using bestgen.Services;
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

    public IActionResult Index()
    {
        return View(_reportService.GetReportCards());
    }

    public IActionResult Details(string id, DateTime? fromDate, DateTime? toDate)
    {
        var report = _reportService.GetReportCards().FirstOrDefault(card => card.ReportKey == id);
        if (report is null)
        {
            return NotFound();
        }

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
        return View(report);
    }
}
