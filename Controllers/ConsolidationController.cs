using bestgen.Services.Consolidation;
using bestgen.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin,Accountant")]
public class ConsolidationController : Controller
{
    private readonly ConsolidationService _service;
    private readonly ITenantContext _tenant;

    public ConsolidationController(ConsolidationService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var f = from?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
        var t = (to?.Date ?? DateTime.UtcNow.Date).AddDays(1);

        var org = await _service.GetCurrentOrgAsync(_tenant.TenantId);
        if (org is null)
        {
            ViewBag.NoOrg = true;
            return View();
        }

        var tenantIds = org.Tenants.Select(x => x.Id).ToList();
        var report = await _service.RollupAsync(tenantIds, f, t);
        ViewBag.Org = org;
        ViewBag.From = f;
        ViewBag.To = t.AddDays(-1);
        return View(report);
    }
}
