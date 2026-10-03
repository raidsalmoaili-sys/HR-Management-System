using Microsoft.AspNetCore.Mvc;
using Project_HR_1.Services.I18n;

namespace Project_HR_1.Controllers;

public class LanguageController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string lang, string? returnUrl = null)
    {
        var safe = lang == "en" ? "en" : "ar";
        Response.Cookies.Append(HrLocalizer.CookieName, safe, new CookieOptions
        {
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false,
            SameSite = SameSiteMode.Lax,
        });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return LocalRedirect("/");
    }
}
