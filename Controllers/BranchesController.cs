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
public class BranchesController : Controller
{
    private const string Svc = "manage-branches";

    private readonly AppDbContext _db;
    private readonly IPermissionService _perms;
    private readonly IScopeContext _scope;
    private readonly IActivityLogger _activity;

    public BranchesController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
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
        var q = _scope.Filter(_db.Branches.AsNoTracking()).Include(b => b.Company).AsQueryable();
        if (companyId.HasValue) q = q.Where(b => b.CompanyId == companyId.Value);
        var branches = await q.OrderBy(b => b.NameAr).ToListAsync();
        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);
        var companies = await companiesQuery.ToListAsync();
        ViewData["Title"] = "Branches";
        ViewData["CurrentServiceId"] = Svc;
        return View(new BranchesListVM
        {
            Branches = branches,
            Companies = companies,
            CompanyFilter = companyId,
            CanCreate = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current),
            CanEdit   = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current),
            CanDelete = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BranchEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        if (_scope.Current.CompanyId.HasValue && m.CompanyId != _scope.Current.CompanyId.Value) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));

        var b = new Branch
        {
            CompanyId = m.CompanyId,
            Slug      = GenerateSlug(m.NameAr),
            NameAr    = m.NameAr, NameEn = m.NameEn,
            IsPrimary = m.IsPrimary,
            LicenseNumber = m.LicenseNumber, LicenseStartDate = m.LicenseStartDate, LicenseEndDate = m.LicenseEndDate,
            VatNumber     = m.VatNumber,     VatStartDate     = m.VatStartDate,     VatEndDate     = m.VatEndDate,
            NatAddressShort = m.NatAddressShort, NatAddressRegDate = m.NatAddressRegDate,
            BuildingNumber  = m.BuildingNumber,  StreetName = m.StreetName,
            SecondaryNumber = m.SecondaryNumber, DistrictName = m.DistrictName,
            PostalCode = m.PostalCode, City = m.City,
        };
        _db.Branches.Add(b);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Branch", b.Id.ToString(), "Create", companyId: m.CompanyId);
        return RedirectToAction(nameof(Index), new { companyId = m.CompanyId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BranchEditVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        if (_scope.Current.BranchId.HasValue && id != _scope.Current.BranchId.Value) return Forbid();
        if (_scope.Current.CompanyId.HasValue && m.CompanyId != _scope.Current.CompanyId.Value) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));
        var b = await _db.Branches.FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();

        b.CompanyId = m.CompanyId;
        b.NameAr    = m.NameAr; b.NameEn = m.NameEn;
        b.IsPrimary = m.IsPrimary;
        b.LicenseNumber = m.LicenseNumber; b.LicenseStartDate = m.LicenseStartDate; b.LicenseEndDate = m.LicenseEndDate;
        b.VatNumber     = m.VatNumber;     b.VatStartDate     = m.VatStartDate;     b.VatEndDate     = m.VatEndDate;
        b.NatAddressShort = m.NatAddressShort; b.NatAddressRegDate = m.NatAddressRegDate;
        b.BuildingNumber  = m.BuildingNumber;  b.StreetName = m.StreetName;
        b.SecondaryNumber = m.SecondaryNumber; b.DistrictName = m.DistrictName;
        b.PostalCode = m.PostalCode; b.City = m.City;
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Branch", b.Id.ToString(), "Update", companyId: m.CompanyId);
        return RedirectToAction(nameof(Index), new { companyId = m.CompanyId });
    }

    private static string GenerateSlug(string nameAr)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        if (string.IsNullOrWhiteSpace(nameAr)) return suffix;
        var base_ = new string(nameAr.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (base_.Length > 56) base_ = base_[..56];
        return $"{base_}-{suffix}";
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete, _scope.Current)) return Forbid();
        if (_scope.Current.BranchId.HasValue && id != _scope.Current.BranchId.Value) return Forbid();
        var b = await _db.Branches.FirstOrDefaultAsync(x => x.Id == id);
        if (b == null) return NotFound();
        if (_scope.Current.CompanyId.HasValue && b.CompanyId != _scope.Current.CompanyId.Value) return Forbid();

        var companyId = b.CompanyId;

        bool hasEmployees = await _db.Employees.AnyAsync(e => e.BranchId == id);
        if (hasEmployees)
        {
            TempData["Error"] = "لا يمكن حذف الفرع لأنه يحتوي على موظفين. انقل الموظفين أو احذفهم أولاً.";
            return RedirectToAction(nameof(Index), new { companyId });
        }

        // null out scoped role assignments before delete
        var assignments = await _db.EmployeeRoleAssignments.Where(a => a.BranchId == id).ToListAsync();
        foreach (var a in assignments) a.BranchId = null;

        _db.Branches.Remove(b);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("Branch", id.ToString(), "Delete");
        return RedirectToAction(nameof(Index), new { companyId });
    }
}
