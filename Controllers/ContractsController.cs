using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Modules;
using Project_HR_1.Models.ViewModels.Modules;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

/// <summary>العقود — employment contracts: issue, renew, terminate, print, expiry tracking.</summary>
public class ContractsController : HrModuleControllerBase
{
    public const int ExpiryWarningDays = 60;

    protected override string ServiceId => "contracts";

    public ContractsController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
        : base(db, perms, scope, activity) { }

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null, string? q = null, bool expiring = false)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var soon = today.AddDays(ExpiryWarningDays);

        var all = Scope.Filter(Db.EmploymentContracts.AsNoTracking());
        var query = all.Include(c => c.Employee).ThenInclude(e => e!.Branch).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(c => c.Status == status);
        if (expiring) query = query.Where(c => c.Status == ContractStatuses.Active && c.EndDate != null && c.EndDate <= soon);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(c => c.ContractNumber.Contains(t) || c.Employee!.NameAr.Contains(t) || c.Employee.NameEn.Contains(t) || c.Employee.EmployeeCode.Contains(t));
        }

        var activeEmpIds = await all.Where(c => c.Status == ContractStatuses.Active).Select(c => c.EmployeeId).Distinct().ToListAsync();
        var working = await ScopedWorkingEmployees().Select(e => e.Id).ToListAsync();

        return View(new ContractsListVM
        {
            Contracts = await query.OrderBy(c => c.Status == ContractStatuses.Active ? 0 : 1)
                                   .ThenBy(c => c.EndDate).Take(400).ToListAsync(),
            Employees = await EmployeePicksAsync(),
            Status = status, Search = q, ExpiringOnly = expiring,
            ActiveCount = await all.CountAsync(c => c.Status == ContractStatuses.Active),
            Expiring60 = await all.CountAsync(c => c.Status == ContractStatuses.Active && c.EndDate != null && c.EndDate >= today && c.EndDate <= soon),
            Expired = await all.CountAsync(c => c.Status == ContractStatuses.Active && c.EndDate != null && c.EndDate < today),
            WithoutContract = working.Count(id => !activeEmpIds.Contains(id)),
            Perms = await PermissionsAsync(),
        });
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id = null, int? employeeId = null, int? renewFrom = null)
    {
        if (!await CanAsync(id.HasValue ? HrBusinessPermissions.Edit : HrBusinessPermissions.Create)) return Forbid();
        var vm = new ContractFormVM { Employees = await EmployeePicksAsync() };

        if (id.HasValue)
        {
            var c = await Scope.Filter(Db.EmploymentContracts.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            Fill(vm, c);
            vm.Id = c.Id;
        }
        else if (renewFrom.HasValue)
        {
            var old = await Scope.Filter(Db.EmploymentContracts.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == renewFrom);
            if (old == null) return NotFound();
            Fill(vm, old);
            vm.RenewFromId = old.Id;
            var start = (old.EndDate ?? DateOnly.FromDateTime(DateTime.Today)).AddDays(1);
            vm.StartDate = start;
            vm.EndDate = old.ContractType == ContractType.Limited ? start.AddYears(1).AddDays(-1) : null;
            vm.ProbationDays = 0;   // no probation on renewal
        }
        else if (employeeId.HasValue)
        {
            var e = await ScopedEmployeeAsync(employeeId.Value);
            if (e == null) return NotFound();
            vm.EmployeeId = e.Id;
            vm.JobTitleAr = e.JobTitleAr;
            vm.BasicSalary = e.BasicSalary;
            vm.HousingAllowance = e.Financial?.HousingAllowance ?? Math.Round(e.BasicSalary * 0.25m);
            vm.TransportAllowance = e.Financial?.TransportAllowance ?? Math.Round(e.BasicSalary * 0.10m);
            vm.OtherAllowance = e.Financial?.OtherAllowance ?? 0;
            vm.ContractType = e.Work?.ContractType ?? ContractType.Limited;
            if (e.ContractStartDate.HasValue) vm.StartDate = e.ContractStartDate.Value;
            vm.EndDate = vm.ContractType == ContractType.Limited ? (e.ContractEndDate ?? vm.StartDate.AddYears(1).AddDays(-1)) : null;
            vm.ProbationDays = e.Work?.ProbationDays ?? 90;
            vm.SelectedEmployee = e;
        }
        return View(vm);
    }

    private static void Fill(ContractFormVM vm, EmploymentContract c)
    {
        vm.EmployeeId = c.EmployeeId; vm.ContractType = c.ContractType; vm.StartDate = c.StartDate; vm.EndDate = c.EndDate;
        vm.JobTitleAr = c.JobTitleAr; vm.BasicSalary = c.BasicSalary; vm.HousingAllowance = c.HousingAllowance;
        vm.TransportAllowance = c.TransportAllowance; vm.OtherAllowance = c.OtherAllowance; vm.ProbationDays = c.ProbationDays;
        vm.AnnualLeaveDays = c.AnnualLeaveDays; vm.WeeklyWorkDays = c.WeeklyWorkDays; vm.DailyWorkHours = c.DailyWorkHours;
        vm.NoticePeriodDays = c.NoticePeriodDays; vm.ExtraTerms = c.ExtraTerms;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ContractFormVM m)
    {
        if (!await CanAsync(m.Id.HasValue ? HrBusinessPermissions.Edit : HrBusinessPermissions.Create)) return Forbid();
        if (m.ContractType == ContractType.Limited && (m.EndDate == null || m.EndDate <= m.StartDate))
            ModelState.AddModelError(nameof(m.EndDate), "End date is required for a limited contract and must be after the start date.");
        if (m.ContractType == ContractType.Unlimited) m.EndDate = null;

        var emp = await ScopedEmployeeAsync(m.EmployeeId);
        if (emp == null) ModelState.AddModelError(nameof(m.EmployeeId), "Employee not found in the current scope.");

        if (!ModelState.IsValid)
        {
            m.Employees = await EmployeePicksAsync();
            m.SelectedEmployee = emp;
            return View("Form", m);
        }

        EmploymentContract c;
        if (m.Id.HasValue)
        {
            c = await Scope.Filter(Db.EmploymentContracts).FirstOrDefaultAsync(x => x.Id == m.Id) ?? throw new InvalidOperationException();
            if (c.Status is ContractStatuses.Renewed or ContractStatuses.Terminated)
            {
                Flash("لا يمكن تعديل عقد مُجدَّد أو مُنهى", "Renewed/terminated contracts cannot be edited", true);
                return RedirectToAction(nameof(Index));
            }
        }
        else
        {
            c = new EmploymentContract
            {
                ContractNumber = await NextNumberAsync(m.StartDate.Year),
                CreatedByUserId = CurrentUserId,
                Status = ContractStatuses.Active,
                RenewedFromId = m.RenewFromId,
            };
            Db.EmploymentContracts.Add(c);

            // Only one active contract per employee: the previous one becomes renewed/superseded.
            var previous = await Db.EmploymentContracts
                .Where(x => x.EmployeeId == emp!.Id && x.Status == ContractStatuses.Active).ToListAsync();
            foreach (var p in previous) p.Status = ContractStatuses.Renewed;
        }

        c.EmployeeId = emp!.Id; c.CompanyId = emp.CompanyId; c.BranchId = emp.BranchId;
        c.ContractType = m.ContractType; c.StartDate = m.StartDate; c.EndDate = m.EndDate;
        c.JobTitleAr = m.JobTitleAr?.Trim(); c.BasicSalary = m.BasicSalary; c.HousingAllowance = m.HousingAllowance;
        c.TransportAllowance = m.TransportAllowance; c.OtherAllowance = m.OtherAllowance; c.ProbationDays = m.ProbationDays;
        c.AnnualLeaveDays = m.AnnualLeaveDays; c.WeeklyWorkDays = m.WeeklyWorkDays; c.DailyWorkHours = m.DailyWorkHours;
        c.NoticePeriodDays = m.NoticePeriodDays; c.ExtraTerms = m.ExtraTerms?.Trim();

        if (m.SyncEmployee)
        {
            // Keep the employee file consistent with the contract that is in force.
            emp.ContractStartDate = c.StartDate;
            emp.ContractEndDate = c.EndDate;
            emp.BasicSalary = c.BasicSalary;
            if (!string.IsNullOrWhiteSpace(c.JobTitleAr)) emp.JobTitleAr = c.JobTitleAr;
            emp.Financial ??= new EmployeeFinancial { EmployeeId = emp.Id };
            emp.Financial.HousingAllowance = c.HousingAllowance;
            emp.Financial.TransportAllowance = c.TransportAllowance;
            emp.Financial.OtherAllowance = c.OtherAllowance;
            emp.Work ??= new EmployeeWork { EmployeeId = emp.Id };
            emp.Work.ContractType = c.ContractType;
            emp.Work.ProbationDays = c.ProbationDays;
        }

        await Db.SaveChangesAsync();
        await Activity.LogAsync("EmploymentContract", c.Id.ToString(), m.Id.HasValue ? "Update" : (m.RenewFromId.HasValue ? "Renew" : "Create"),
            employeeId: emp.Id, companyId: emp.CompanyId, branchId: emp.BranchId);
        Flash($"تم حفظ العقد {c.ContractNumber}", $"Contract {c.ContractNumber} saved");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate(int id, DateOnly? date, string? reason)
    {
        if (!await CanAsync(HrBusinessPermissions.Edit)) return Forbid();
        var c = await Scope.Filter(Db.EmploymentContracts).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (c.Status != ContractStatuses.Active) return RedirectToAction(nameof(Index));
        c.Status = ContractStatuses.Terminated;
        c.TerminatedOn = date ?? DateOnly.FromDateTime(DateTime.Today);
        c.TerminationReason = reason?.Trim();
        await Db.SaveChangesAsync();
        await Activity.LogAsync("EmploymentContract", id.ToString(), "Terminate", employeeId: c.EmployeeId, companyId: c.CompanyId, branchId: c.BranchId);
        Flash("تم إنهاء العقد", "Contract terminated");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.Delete)) return Forbid();
        var c = await Scope.Filter(Db.EmploymentContracts).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        Db.EmploymentContracts.Remove(c);
        await Db.SaveChangesAsync();
        await Activity.LogAsync("EmploymentContract", id.ToString(), "Delete", employeeId: c.EmployeeId, companyId: c.CompanyId);
        Flash("تم حذف العقد", "Contract deleted");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var c = await Scope.Filter(Db.EmploymentContracts.AsNoTracking())
            .Include(x => x.Employee).ThenInclude(e => e!.Personal)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return View(new ContractPrintVM
        {
            Contract = c,
            Company = await Db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.CompanyId),
            Branch = await Db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.BranchId),
        });
    }

    private async Task<string> NextNumberAsync(int year)
    {
        var prefix = $"CT-{year}-";
        var last = await Db.EmploymentContracts
            .Where(c => c.ContractNumber.StartsWith(prefix))
            .Select(c => c.ContractNumber)
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync();
        var next = last != null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{next:0000}";
    }
}
