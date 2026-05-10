using System.Security.Claims;
using bestgen.Data;
using bestgen.Models;
using bestgen.Services.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin,Accountant,Sales,Purchases,Warehouse")]
public class ImportController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CsvImportService _service;

    public ImportController(ApplicationDbContext db, CsvImportService service)
    {
        _db = db;
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var jobs = await _db.ImportJobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedAt).Take(50).ToListAsync();
        return View(jobs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Preview(ImportEntity entity, IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ImportError"] = "Choose a CSV file.";
            return RedirectToAction(nameof(Index));
        }
        using var reader = new StreamReader(file.OpenReadStream());
        var csv = await reader.ReadToEndAsync();

        var preview = _service.Preview(entity, file.FileName, csv);
        if (preview.Error is not null)
        {
            TempData["ImportError"] = preview.Error;
            return RedirectToAction(nameof(Index));
        }

        // Stash the CSV text for the next step using TempData (size cap ~ a few MB).
        TempData["ImportCsv"] = csv;
        TempData["ImportFileName"] = file.FileName;
        TempData["ImportEntity"] = entity.ToString();
        ViewBag.Preview = preview;
        ViewBag.Fields = _service.EntityFields(entity);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Commit(ImportEntity entity, string? fileName, List<string?> mapping)
    {
        var csv = TempData["ImportCsv"] as string;
        if (string.IsNullOrEmpty(csv))
        {
            TempData["ImportError"] = "Session expired — re-upload the file.";
            return RedirectToAction(nameof(Index));
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var job = await _service.CommitAsync(entity, fileName ?? "uploaded.csv", csv, mapping, userId);
        TempData["ImportMessage"] =
            $"{entity}: imported {job.ImportedRows}, skipped {job.SkippedRows}, failed {job.FailedRows} of {job.TotalRows} rows.";
        return RedirectToAction(nameof(Index));
    }
}
