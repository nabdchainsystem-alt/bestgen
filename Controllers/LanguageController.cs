using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace bestgen.Controllers;

public class LanguageController : Controller
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "en-US", "ar-SA"
    };

    [HttpGet]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult Set(string? culture, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(culture) || !Supported.Contains(culture))
        {
            culture = "ar-SA";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = false
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }
}
