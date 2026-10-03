using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Modules;
using Project_HR_1.Models.ViewModels.Modules;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

/// <summary>السلف — salary advances repaid in monthly installments through the payroll run.</summary>
public class AdvancesController : HrModuleControllerBase
{
    /// <summary>An advance may not exceed this many months of basic salary.</summary>
    public const int MaxMonthsOfSalary = 3;

    protected override string ServiceId => "advances";

    public AdvancesController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
        : base(db, perms, scope, activity) { }

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null, string? q = null)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();

        var all = Scope.Filter(Db.SalaryAdvances.AsNoTracking());
        var query = all.Include(a => a.Employee).ThenInclude(e => e!.Branch).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(a => a.Employee!.NameAr.Contains(t) || a.Employee.NameEn.Contains(t) || a.Employee.EmployeeCode.Contains(t));
        }

        var approved = await all.Where(a => a.Status == RequestStatuses.Approved).ToListAsync();
        return View(new AdvancesListVM
        {
            Advances = await query.OrderBy(a => a.Status == RequestStatuses.Pending ? 0 : a.Status == RequestStatuses.Approved ? 1 : 2)
                                  .ThenByDescending(a => a.CreatedAt).Take(300).ToListAsync(),
            Employees = await EmployeePicksAsync(),
            Status = status, Search = q,
            PendingCount = await all.CountAsync(a => a.Status == RequestStatuses.Pending),
            ActiveCount = approved.Count,
            OutstandingTotal = approved.Sum(a => a.Remaining),
            RepaidTotal = await all.Where(a => a.Status == RequestStatuses.Approved || a.Status == RequestStatuses.Closed).SumAsync(a => a.RepaidAmount),
            Perms = await PermissionsAsync(),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdvanceCreateVM m)
    {
        if (!await CanAsync(HrBusinessPermissions.Create)) return Forbid();
        if (!ModelState.IsValid)
        {
            Flash("تحقق من المبلغ وعدد الأقساط", "Check the amount and installments", true);
            return RedirectToAction(nameof(Index));
        }
        var emp = await ScopedEmployeeAsync(m.EmployeeId);
        if (emp == null) return NotFound();

        if (emp.BasicSalary > 0 && m.Amount > emp.BasicSalary * MaxMonthsOfSalary)
        {
            Flash($"الحد الأعلى للسلفة {MaxMonthsOfSalary} رواتب أساسية ({emp.BasicSalary * MaxMonthsOfSalary:#,##0} ر.س)",
                  $"Max advance is {MaxMonthsOfSalary}× basic salary", true);
            return RedirectToAction(nameof(Index));
        }
        if (await Db.SalaryAdvances.AnyAsync(a => a.EmployeeId == emp.Id && (a.Status == RequestStatuses.Pending || a.Status == RequestStatuses.Approved)))
        {
            Flash("لدى الموظف سلفة قائمة لم تُسدد بعد", "The employee already has an open advance", true);
            return RedirectToAction(nameof(Index));
        }

        var adv = new SalaryAdvance
        {
            EmployeeId = emp.Id, CompanyId = emp.CompanyId, BranchId = emp.BranchId,
            Amount = m.Amount, Installments = m.Installments,
            MonthlyInstallment = Math.Ceiling(m.Amount / m.Installments * 100) / 100,
            StartYear = m.StartYear, StartMonth = m.StartMonth,
            Reason = m.Reason?.Trim(), CreatedByUserId = CurrentUserId,
        };
        Db.SalaryAdvances.Add(adv);
        await Db.SaveChangesAsync();
        await Activity.LogAsync("SalaryAdvance", adv.Id.ToString(), "Create", employeeId: emp.Id, companyId: emp.CompanyId, branchId: emp.BranchId);
        Flash("تم تسجيل طلب السلفة وهو بانتظار الاعتماد", "Advance request created");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? note) => await Review(id, RequestStatuses.Approved, note);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? note) => await Review(id, RequestStatuses.Rejected, note);

    private async Task<IActionResult> Review(int id, string status, string? note)
    {
        if (!await CanAsync(HrBusinessPermissions.Approve)) return Forbid();
        var adv = await Scope.Filter(Db.SalaryAdvances).FirstOrDefaultAsync(a => a.Id == id);
        if (adv == null) return NotFound();
        if (adv.Status != RequestStatuses.Pending) return RedirectToAction(nameof(Index));
        adv.Status = status;
        adv.ReviewedByUserId = CurrentUserId;
        adv.ReviewedAt = DateTime.UtcNow;
        adv.ReviewNote = note?.Trim();
        await Db.SaveChangesAsync();
        await Activity.LogAsync("SalaryAdvance", id.ToString(), status == RequestStatuses.Approved ? "Approve" : "Reject",
            employeeId: adv.EmployeeId, companyId: adv.CompanyId, branchId: adv.BranchId);
        if (status == RequestStatuses.Approved)
            Flash("تم اعتماد السلفة — ستُخصم الأقساط تلقائياً في مسير الرواتب", "Advance approved — installments are deducted in payroll");
        else
            Flash("تم رفض السلفة", "Advance rejected");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.Delete)) return Forbid();
        var adv = await Scope.Filter(Db.SalaryAdvances).FirstOrDefaultAsync(a => a.Id == id);
        if (adv == null) return NotFound();
        if (adv.RepaidAmount > 0)
        {
            Flash("لا يمكن حذف سلفة بدأ سدادها", "Cannot delete an advance that is being repaid", true);
            return RedirectToAction(nameof(Index));
        }
        Db.SalaryAdvances.Remove(adv);
        await Db.SaveChangesAsync();
        await Activity.LogAsync("SalaryAdvance", id.ToString(), "Delete", employeeId: adv.EmployeeId, companyId: adv.CompanyId);
        Flash("تم حذف السلفة", "Advance deleted");
        return RedirectToAction(nameof(Index));
    }
}
