using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.ViewModels.Management;
using Project_HR_1.Services.Permissions;

namespace Project_HR_1.Controllers;

[Authorize]
public class LoginLogController : Controller
{
    private const string Svc = "view-login-log";

    private readonly AppDbContext _db;
    private readonly IPermissionService _perms;

    public LoginLogController(AppDbContext db, IPermissionService perms)
    {
        _db = db;
        _perms = perms;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.View)) return Forbid();
        var entries = await _db.LoginLogs.AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .Take(200)
            .ToListAsync();
        ViewData["Title"] = "Login Log";
        ViewData["CurrentServiceId"] = Svc;
        return View(new LoginLogListVM { Entries = entries });
    }
}
