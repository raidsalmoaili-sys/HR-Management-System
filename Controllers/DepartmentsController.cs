using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.ViewModels.Management;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

[Authorize]
public class DepartmentsController : Controller
{
    private const string Svc = "manage-departments";

    private readonly AppDbContext _db;
    private readonly IPermissionService _perms;
    private readonly IScopeContext _scope;
    private readonly IActivityLogger _activity;

    public DepartmentsController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
    {
        _db = db;
        _perms = perms;
        _scope = scope;
        _activity = activity;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? companyId = null)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.View, _scope.Current)) return Forbid();
        var q = _scope.Filter(_db.Departments.AsNoTracking()).Include(d => d.Company).AsQueryable();
        if (companyId.HasValue) q = q.Where(d => d.CompanyId == companyId.Value);
        var departments = await q.OrderBy(d => d.NameAr).ToListAsync();
        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);
        var companies = await companiesQuery.ToListAsync();
        ViewData["Title"] = "Departments";
        ViewData["CurrentServiceId"] = Svc;
        return View(new DepartmentsListVM
        {
            Departments = departments,
            Companies = companies,
            CompanyFilter = companyId,
            CanCreate = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current),
            CanEdit   = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current),
            CanDelete = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartmentEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        if (_scope.Current.CompanyId.HasValue && m.CompanyId != _scope.Current.CompanyId.Value) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        var d = new Department { CompanyId = m.CompanyId, NameAr = m.NameAr, NameEn = m.NameEn, IsActive = m.IsActive };
        _db.Departments.Add(d);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Department", d.Id.ToString(), "Create", companyId: m.CompanyId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DepartmentEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        if (_scope.Current.CompanyId.HasValue && m.CompanyId != _scope.Current.CompanyId.Value) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        var d = await _scope.Filter(_db.Departments).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();
        d.CompanyId = m.CompanyId; d.NameAr = m.NameAr; d.NameEn = m.NameEn; d.IsActive = m.IsActive;
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Department", d.Id.ToString(), "Update", companyId: m.CompanyId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current)) return Forbid();
        var d = await _scope.Filter(_db.Departments).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return NotFound();
        _db.Departments.Remove(d);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Department", id.ToString(), "Delete");
        return RedirectToAction(nameof(Index));
    }
}
