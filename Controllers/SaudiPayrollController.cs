using bestgen.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

[Authorize]
public class SaudiPayrollController : Controller
{
    private readonly SaudiPayrollService _service;

    public SaudiPayrollController(SaudiPayrollService service) { _service = service; }

    public async Task<IActionResult> Gosi()
    {
        var rows = await _service.CalculateGosiForActiveAsync();
        return View(rows);
    }

    public IActionResult Eosb() => View();

    [HttpGet]
    public async Task<IActionResult> EosbCalc(int employeeId, DateTime? endDate, decimal? resignationFactor)
    {
        var end = endDate ?? DateTime.UtcNow.Date;
        var factor = resignationFactor.GetValueOrDefault(1m);
        var result = await _service.CalculateEosbForEmployeeAsync(employeeId, end, factor);
        if (result is null) return NotFound();
        return Json(result);
    }
}
