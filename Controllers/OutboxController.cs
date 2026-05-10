using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

/// <summary>
/// Server-side shell for the offline outbox UI. The actual data lives in the
/// browser's IndexedDB — this controller just renders a page that calls
/// <c>window.bestgenOutbox</c> to render queued items.
/// </summary>
[Authorize]
public class OutboxController : Controller
{
    public IActionResult Index() => View();
}
