using bestgen.Services.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly ISearchService _search;

    public SearchController(ISearchService search) { _search = search; }

    public async Task<IActionResult> Index(string? q)
    {
        var results = await _search.SearchAsync(q ?? "");
        return View(results);
    }

    /// <summary>JSON endpoint for inline / autocomplete dropdown.</summary>
    [HttpGet]
    public async Task<IActionResult> Autocomplete(string? q)
    {
        var results = await _search.SearchAsync(q ?? "");
        return Json(new
        {
            query = results.Query,
            total = results.Total,
            groups = results.ByType.Select(kv => new
            {
                type = kv.Key,
                hits = kv.Value.Select(h => new { h.Title, h.Subtitle, h.Url, h.EntityType, h.Id })
            })
        });
    }
}
