using bestgen.Data;
using bestgen.Models;
using bestgen.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class RecurringInvoicesController : CrudController<RecurringInvoice>
{
    private readonly RecurringInvoiceService _service;

    public RecurringInvoicesController(ApplicationDbContext context, RecurringInvoiceService service)
        : base(context)
    {
        _service = service;
    }

    protected override IQueryable<RecurringInvoice> Query() => Context.RecurringInvoices
        .AsNoTracking()
        .Include(r => r.Customer)
        .Include(r => r.Product);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id)
    {
        var invoice = await _service.GenerateOneByIdAsync(id);
        if (invoice is null)
        {
            TempData["RecurringError"] = "Template not found.";
            return RedirectToAction("Index");
        }
        TempData["RecurringMessage"] = $"Generated invoice {invoice.InvoiceNumber}.";
        return RedirectToAction("Details", "SalesInvoices", new { id = invoice.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAll()
    {
        var generated = await _service.RunDueAsync();
        TempData["RecurringMessage"] = $"Generated {generated} invoice(s).";
        return RedirectToAction("Index");
    }
}
