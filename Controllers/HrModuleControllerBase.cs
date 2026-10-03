using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;
using System.Security.Claims;

namespace Project_HR_1.Controllers;

/// <summary>
/// Shared plumbing for the business-service controllers (attendance, leaves, payroll, contracts, advances,
/// dashboard): permission checks against the service id, the current scope, and scoped lookups.
/// </summary>
[Authorize]
public abstract class HrModuleControllerBase : Controller
{
    protected readonly AppDbContext Db;
    protected readonly IPermissionService Perms;
    protected readonly IScopeContext Scope;
    protected readonly IActivityLogger Activity;

    protected abstract string ServiceId { get; }

    protected HrModuleControllerBase(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
    {
        Db = db;
        Perms = perms;
        Scope = scope;
        Activity = activity;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ViewData["CurrentServiceId"] = ServiceId;
        base.OnActionExecuting(context);
    }

    protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected Task<bool> CanAsync(string permission) => Perms.CanAsync(User, ServiceId, permission, Scope.Current);

    protected async Task<ModulePermissions> PermissionsAsync() => new()
    {
        CanCreate  = await CanAsync(HrBusinessPermissions.Create),
        CanEdit    = await CanAsync(HrBusinessPermissions.Edit),
        CanDelete  = await CanAsync(HrBusinessPermissions.Delete),
        CanApprove = await CanAsync(HrBusinessPermissions.Approve),
    };

    /// <summary>Companies visible in the current scope (for filters and pickers).</summary>
    protected async Task<List<Company>> ScopedCompaniesAsync()
    {
        var q = Db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (Scope.Current.CompanyId.HasValue) q = q.Where(c => c.Id == Scope.Current.CompanyId.Value);
        return await q.ToListAsync();
    }

    protected async Task<List<Branch>> ScopedBranchesAsync(int? companyId = null)
    {
        var q = Scope.Filter(Db.Branches.AsNoTracking());
        if (companyId.HasValue) q = q.Where(b => b.CompanyId == companyId.Value);
        return await q.OrderBy(b => b.CompanyId).ThenBy(b => b.NameAr).ToListAsync();
    }

    /// <summary>Employees in the current scope that are still on the payroll (active / on leave).</summary>
    protected IQueryable<Employee> ScopedWorkingEmployees() =>
        Scope.Filter(Db.Employees.AsNoTracking())
            .Where(e => e.Status == EmployeeStatuses.Active || e.Status == EmployeeStatuses.Leave);

    /// <summary>Loads an employee only if it is inside the current scope.</summary>
    protected Task<Employee?> ScopedEmployeeAsync(int id) =>
        Scope.Filter(Db.Employees).Include(e => e.Financial).Include(e => e.Work).FirstOrDefaultAsync(e => e.Id == id);

    /// <summary>Compact list of in-scope working employees for the "choose employee" pickers.</summary>
    protected async Task<List<Models.ViewModels.Modules.EmployeePick>> EmployeePicksAsync() =>
        await ScopedWorkingEmployees()
            .OrderBy(e => e.NameAr)
            .Select(e => new Models.ViewModels.Modules.EmployeePick(
                e.Id, e.EmployeeCode, e.NameAr, e.NameEn,
                e.Branch != null ? e.Branch.NameAr : null,
                e.Work != null ? e.Work.AnnualLeaveBalance : 0,
                e.BasicSalary))
            .ToListAsync();

    protected void Flash(string ar, string en, bool error = false) =>
        TempData[error ? "ModuleError" : "ModuleOk"] = $"{ar}|{en}";
}

public class ModulePermissions
{
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
}
