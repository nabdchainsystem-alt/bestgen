using bestgen.Services.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

[Authorize]
public class OnboardingController : Controller
{
    private readonly IndustryTemplateService _templates;

    public OnboardingController(IndustryTemplateService templates)
    {
        _templates = templates;
    }

    public IActionResult Index()
    {
        ViewBag.Catalog = _templates.Catalog;
        return View();
    }

    public IActionResult Details(IndustryTemplate template)
    {
        var info = _templates.Catalog.FirstOrDefault(t => t.Template == template);
        if (info is null) return NotFound();
        var preview = _templates.Preview(template);
        ViewBag.Info = info;
        ViewBag.Preview = preview;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(IndustryTemplate template)
    {
        var result = await _templates.ApplyAsync(template);
        TempData["TemplateMessage"] =
            $"Applied {template} template — {result.CategoriesAdded} categories, {result.ProductsAdded} products, {result.AccountsAdded} accounts added.";
        return RedirectToAction(nameof(Index));
    }
}
