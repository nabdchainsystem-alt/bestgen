using bestgen.Models;
using bestgen.Services.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

[Authorize]
public class AttachmentsController : Controller
{
    private readonly AttachmentService _attachments;

    public AttachmentsController(AttachmentService attachments)
    {
        _attachments = attachments;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Upload(AttachmentDocumentType docType, int docId, IFormFile file, string? description, string? returnUrl)
    {
        var name = User.Identity?.IsAuthenticated == true ? User.Identity!.Name : null;
        var uid = User.FindFirst("sub")?.Value;
        var (ok, err, _) = await _attachments.UploadAsync(docType, docId, file, uid, name, description);
        if (ok) TempData["AttachmentMessage"] = "File uploaded.";
        else TempData["AttachmentError"] = err;
        return SafeRedirect(returnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var (bytes, name, ct) = await _attachments.ReadAsync(id);
        if (bytes is null || name is null) return NotFound();
        return File(bytes, ct ?? "application/octet-stream", name);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var ok = await _attachments.DeleteAsync(id);
        if (ok) TempData["AttachmentMessage"] = "File deleted.";
        else TempData["AttachmentError"] = "Attachment not found.";
        return SafeRedirect(returnUrl);
    }

    private IActionResult SafeRedirect(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
