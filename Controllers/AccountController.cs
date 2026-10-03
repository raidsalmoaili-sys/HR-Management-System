using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.Audit;
using Project_HR_1.Services.Audit;

namespace Project_HR_1.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly IActivityLogger _activity;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext db,
        IActivityLogger activity)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _activity = activity;
    }

    // ─── Login ───────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Resume", "Entry");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await WriteLoginLogAsync(model.Email, user?.Id, success: true);
            await _activity.LogAsync("Login", user!.Id, "Login", userId: user.Id);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction("Resume", "Entry");
        }

        await WriteLoginLogAsync(model.Email, user?.Id, success: false,
            failureReason: result.IsLockedOut ? "LockedOut" : "InvalidCredentials");
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var uid = _userManager.GetUserId(User);
        await _signInManager.SignOutAsync();
        if (!string.IsNullOrEmpty(uid))
            await _activity.LogAsync("Login", uid, "Logout", userId: uid);
        return RedirectToAction("Index", "Home");
    }

    private async Task WriteLoginLogAsync(string email, string? userId, bool success, string? failureReason = null)
    {
        _db.LoginLogs.Add(new LoginLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            Email = email,
            Success = success,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString(),
            FailureReason = failureReason,
        });
        await _db.SaveChangesAsync();
    }
}
