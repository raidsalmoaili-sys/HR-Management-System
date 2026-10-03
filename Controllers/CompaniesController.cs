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
public class CompaniesController : Controller
{
    private const string Svc = "manage-companies";

    private readonly AppDbContext _db;
    private readonly IPermissionService _perms;
    private readonly IScopeContext _scope;
    private readonly IActivityLogger _activity;

    public CompaniesController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
    {
        _db = db;
        _perms = perms;
        _scope = scope;
        _activity = activity;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.View, _scope.Current)) return Forbid();
        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);
        var companies = await companiesQuery.ToListAsync();
        var ids = companies.Select(c => c.Id).ToList();
        var bc = await _db.Branches
            .Where(b => ids.Contains(b.CompanyId))
            .GroupBy(b => b.CompanyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var ec = await _db.Employees
            .Where(e => ids.Contains(e.CompanyId))
            .GroupBy(e => e.CompanyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        foreach (var c in companies)
        {
            c.BranchesCount  = bc.GetValueOrDefault(c.Id);
            c.EmployeesCount = ec.GetValueOrDefault(c.Id);
        }
        ViewData["Title"] = "Companies";
        ViewData["CurrentServiceId"] = Svc;
        return View(new CompaniesListVM
        {
            Companies = companies,
            CanCreate = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current),
            CanEdit   = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current),
            CanDelete = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));

        var c = new Company
        {
            Slug     = GenerateSlug(m.NameEn),
            Initials = GenerateInitials(m.NameAr),
            NameAr   = m.NameAr, NameEn = m.NameEn,
            UnifiedNumber         = m.UnifiedNumber,
            EstablishmentNumber   = m.EstablishmentNumber,
            CommercialRegistration = m.CommercialRegistration,
            SectorAr = m.SectorAr,
            EstablishedYear = m.EstablishedYear,
            HeadquartersAr = m.HeadquartersAr,
        };
        _db.Companies.Add(c);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Company", c.Id.ToString(), "Create");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CompanyEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        if (_scope.Current.CompanyId.HasValue && id != _scope.Current.CompanyId.Value) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        c.NameAr = m.NameAr; c.NameEn = m.NameEn;
        c.Initials = GenerateInitials(m.NameAr);
        c.UnifiedNumber          = m.UnifiedNumber;
        c.EstablishmentNumber    = m.EstablishmentNumber;
        c.CommercialRegistration = m.CommercialRegistration;
        c.SectorAr = m.SectorAr;
        c.EstablishedYear = m.EstablishedYear;
        c.HeadquartersAr = m.HeadquartersAr;
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Company", c.Id.ToString(), "Update");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current)) return Forbid();
        if (_scope.Current.CompanyId.HasValue && id != _scope.Current.CompanyId.Value) return Forbid();
        var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        bool hasBranches = await _db.Branches.AnyAsync(b => b.CompanyId == id);
        if (hasBranches)
        {
            TempData["Error"] = "لا يمكن حذف الشركة لأنها تحتوي على فروع. احذف الفروع أولاً.";
            return RedirectToAction(nameof(Index));
        }

        _db.Companies.Remove(c);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Company", id.ToString(), "Delete");
        return RedirectToAction(nameof(Index));
    }

    private static string GenerateSlug(string nameEn)
    {
        if (string.IsNullOrWhiteSpace(nameEn)) return Guid.NewGuid().ToString("N")[..8];
        return new string(nameEn.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');
    }

    private static string GenerateInitials(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr)) return "؟";
        var parts = nameAr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][..1]}{parts[1][..1]}"
            : nameAr[..Math.Min(2, nameAr.Length)];
    }
}
