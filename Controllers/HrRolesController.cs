using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.ViewModels.Management;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;

namespace Project_HR_1.Controllers;

[Authorize]
public class HrRolesController : Controller
{
    private const string Svc = "settings-hr-roles";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPermissionService _perms;
    private readonly IActivityLogger _activity;

    public HrRolesController(AppDbContext db, UserManager<ApplicationUser> users, IPermissionService perms, IActivityLogger activity)
    {
        _db = db;
        _users = users;
        _perms = perms;
        _activity = activity;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.View)) return Forbid();
        var roles = await _db.HrRoles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.NameAr).ToListAsync();
        ViewData["Title"] = "Roles";
        ViewData["CurrentServiceId"] = Svc;
        return View(new HrRolesListVM
        {
            Roles = roles,
            CanCreate = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create),
            CanEdit   = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit),
            CanDelete = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HrRoleEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create)) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));

        var role = new HrRole
        {
            Code = m.Code, NameAr = m.NameAr, NameEn = m.NameEn,
            Description = m.Description, IsSystem = false,
        };
        foreach (var c in m.Cells.Where(x => x.Checked))
            role.Permissions.Add(new HrRolePermission { ServiceId = c.ServiceId, Permission = c.Permission });

        _db.HrRoles.Add(role);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("HrRole", role.Id.ToString(), "Create", userId: _users.GetUserId(User));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HrRoleEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit)) return Forbid();
        var role = await _db.HrRoles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));

        role.Code = m.Code; role.NameAr = m.NameAr; role.NameEn = m.NameEn;
        role.Description = m.Description;

        _db.HrRolePermissions.RemoveRange(role.Permissions);
        await _db.SaveChangesAsync();

        foreach (var c in m.Cells.Where(x => x.Checked))
            _db.HrRolePermissions.Add(new HrRolePermission
            {
                HrRoleId = role.Id, ServiceId = c.ServiceId, Permission = c.Permission,
            });
        await _db.SaveChangesAsync();

        await _activity.LogAsync("HrRole", role.Id.ToString(), "Update", userId: _users.GetUserId(User));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete)) return Forbid();
        var role = await _db.HrRoles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound();
        if (role.IsSystem) return BadRequest("Cannot delete a system role.");
        _db.HrRoles.Remove(role);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("HrRole", id.ToString(), "Delete", userId: _users.GetUserId(User));
        return RedirectToAction(nameof(Index));
    }
}
