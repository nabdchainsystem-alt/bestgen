using bestgen.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

[Authorize]
public class BrainController : Controller
{
    private readonly BrainChatService _chat;

    public BrainController(BrainChatService chat) { _chat = chat; }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] BrainAskRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Message))
        {
            return BadRequest(new { error = "Message required." });
        }

        var isArabic = (Request.Headers["Accept-Language"].ToString() ?? "")
                           .StartsWith("ar", StringComparison.OrdinalIgnoreCase)
                       || System.Globalization.CultureInfo.CurrentUICulture.Name
                              .StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        var history = (req.History ?? Array.Empty<BrainChatTurn>())
            .TakeLast(20) // hard cap to keep prompts cheap
            .ToList();

        var reply = await _chat.AskAsync(history, req.Message, isArabic);
        return Json(new
        {
            text = reply.Text,
            tools = reply.ToolsCalled
        });
    }
}

public sealed class BrainAskRequest
{
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<BrainChatTurn>? History { get; set; }
}
