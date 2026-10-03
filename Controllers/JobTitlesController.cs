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
public class JobTitlesController : Controller
{
    private const string Svc = "manage-jobs";

    private readonly AppDbContext _db;
    private readonly IPermissionService _perms;
    private readonly IScopeContext _scope;
    private readonly IActivityLogger _activity;

    public JobTitlesController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
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
        var q = _scope.Filter(_db.JobTitles.AsNoTracking()).Include(j => j.Company).AsQueryable();
        if (companyId.HasValue) q = q.Where(j => j.CompanyId == companyId.Value);
        var titles = await q.OrderBy(j => j.NameAr).ToListAsync();
        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);
        var companies = await companiesQuery.ToListAsync();
        ViewData["Title"] = "Job Titles";
        ViewData["CurrentServiceId"] = Svc;
        return View(new JobTitlesListVM
        {
            JobTitles = titles,
            Companies = companies,
            CompanyFilter = companyId,
            CanCreate = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current),
            CanEdit   = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current),
            CanDelete = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobTitleEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        if (_scope.Current.CompanyId.HasValue && m.CompanyId != _scope.Current.CompanyId.Value) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        var j = new JobTitle
        {
            CompanyId = m.CompanyId, Code = m.Code, NameAr = m.NameAr, NameEn = m.NameEn,
            IsActive = m.IsActive,
        };
        _db.JobTitles.Add(j);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("JobTitle", j.Id.ToString(), "Create", companyId: m.CompanyId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, JobTitleEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        if (_scope.Current.CompanyId.HasValue && m.CompanyId != _scope.Current.CompanyId.Value) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        var j = await _scope.Filter(_db.JobTitles).FirstOrDefaultAsync(x => x.Id == id);
        if (j == null) return NotFound();
        j.CompanyId = m.CompanyId; j.Code = m.Code;
        j.NameAr = m.NameAr; j.NameEn = m.NameEn; j.IsActive = m.IsActive;
        await _db.SaveChangesAsync();
        await _activity.LogAsync("JobTitle", j.Id.ToString(), "Update", companyId: m.CompanyId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current)) return Forbid();
        var j = await _scope.Filter(_db.JobTitles).FirstOrDefaultAsync(x => x.Id == id);
        if (j == null) return NotFound();
        _db.JobTitles.Remove(j);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("JobTitle", id.ToString(), "Delete");
        return RedirectToAction(nameof(Index));
    }
}
