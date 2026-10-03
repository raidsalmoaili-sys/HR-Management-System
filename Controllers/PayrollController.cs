using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Modules;
using Project_HR_1.Models.ViewModels.Modules;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.HrModules;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

/// <summary>مسيرات الرواتب — monthly payroll run per company: calculate, adjust, approve, pay, payslips, bank file.</summary>
public class PayrollController : HrModuleControllerBase
{
    private readonly PayrollCalculator _calc;
    protected override string ServiceId => "payroll";

    public PayrollController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity, PayrollCalculator calc)
        : base(db, perms, scope, activity) => _calc = calc;

    [HttpGet]
    public async Task<IActionResult> Index(int? year = null)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var y = year ?? DateTime.Today.Year;
        var runs = await Scope.Filter(Db.PayrollRuns.AsNoTracking()).Include(r => r.Company)
            .Where(r => r.Year == y).OrderByDescending(r => r.Month).ThenBy(r => r.CompanyId).ToListAsync();

        return View(new PayrollListVM
        {
            Runs = runs,
            Companies = await ScopedCompaniesAsync(),
            Year = y,
            YearNet = runs.Where(r => r.Status != PayrollStatuses.Draft).Sum(r => r.TotalNet),
            LastNet = runs.Where(r => r.Status != PayrollStatuses.Draft).GroupBy(r => r.Month).OrderByDescending(g => g.Key).FirstOrDefault()?.Sum(r => r.TotalNet) ?? 0,
            DraftCount = runs.Count(r => r.Status == PayrollStatuses.Draft),
            Perms = await PermissionsAsync(),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int companyId, int year, int month)
    {
        if (!await CanAsync(HrBusinessPermissions.Create)) return Forbid();
        if (Scope.Current.CompanyId.HasValue && Scope.Current.CompanyId != companyId) return Forbid();
        if (month is < 1 or > 12 || year is < 2000 or > 2100) return BadRequest();

        var existing = await Db.PayrollRuns.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Year == year && r.Month == month);
        if (existing != null)
        {
            Flash("يوجد مسير لهذا الشهر مسبقاً", "A payroll run already exists for this month", true);
            return RedirectToAction(nameof(Details), new { id = existing.Id });
        }

        var run = new PayrollRun { CompanyId = companyId, Year = year, Month = month, CreatedByUserId = CurrentUserId };
        Db.PayrollRuns.Add(run);
        await _calc.BuildItemsAsync(run);
        await Db.SaveChangesAsync();
        await Activity.LogAsync("PayrollRun", run.Id.ToString(), "Create", companyId: companyId, details: run.PeriodLabel);
        Flash($"تم إنشاء مسير {run.PeriodLabel} لعدد {run.EmployeesCount} موظف", $"Payroll {run.PeriodLabel} created for {run.EmployeesCount} employees");
        return RedirectToAction(nameof(Details), new { id = run.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, int? branchId = null)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var run = await LoadRunAsync(id, tracking: false);
        if (run == null) return NotFound();

        var items = run.Items.AsEnumerable();
        if (Scope.Current.Level == ScopeLevel.Branch && Scope.Current.BranchId.HasValue)
            items = items.Where(i => i.BranchId == Scope.Current.BranchId.Value);
        if (branchId.HasValue) items = items.Where(i => i.BranchId == branchId.Value);

        return View(new PayrollRunVM
        {
            Run = run,
            Items = items.OrderBy(i => i.Employee!.NameAr).ToList(),
            BranchNames = await BranchNamesAsync(run.CompanyId),
            BranchId = branchId,
            Perms = await PermissionsAsync(),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAdjustments(int id, List<PayrollAdjustment> items)
    {
        if (!await CanAsync(HrBusinessPermissions.Edit)) return Forbid();
        var run = await LoadRunAsync(id, tracking: true);
        if (run == null) return NotFound();
        if (run.Status != PayrollStatuses.Draft) return RedirectToAction(nameof(Details), new { id });

        foreach (var adj in items)
        {
            var item = run.Items.FirstOrDefault(i => i.Id == adj.ItemId);
            if (item == null) continue;
            item.Additions = Math.Max(0, adj.Additions);
            item.OtherDeductions = Math.Max(0, adj.OtherDeductions);
            item.Notes = string.IsNullOrWhiteSpace(adj.Notes) ? null : adj.Notes.Trim();
        }
        PayrollCalculator.RecalcTotals(run);
        await Db.SaveChangesAsync();
        Flash("تم حفظ التعديلات", "Adjustments saved");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Recalculate(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.Edit)) return Forbid();
        var run = await LoadRunAsync(id, tracking: true);
        if (run == null) return NotFound();
        if (run.Status != PayrollStatuses.Draft) return RedirectToAction(nameof(Details), new { id });
        await _calc.BuildItemsAsync(run);
        await Db.SaveChangesAsync();
        Flash("تمت إعادة الاحتساب من بيانات التحضير والإجازات والسلف", "Recalculated from attendance, leaves and advances");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.Approve)) return Forbid();
        var run = await LoadRunAsync(id, tracking: true);
        if (run == null) return NotFound();
        if (run.Status != PayrollStatuses.Draft) return RedirectToAction(nameof(Details), new { id });

        await _calc.ApplyAdvanceRepaymentsAsync(run);
        run.Status = PayrollStatuses.Approved;
        run.ApprovedAt = DateTime.UtcNow;
        run.ApprovedByUserId = CurrentUserId;
        await Db.SaveChangesAsync();
        await Activity.LogAsync("PayrollRun", id.ToString(), "Approve", companyId: run.CompanyId, details: run.PeriodLabel);
        Flash("تم اعتماد المسير وخصم أقساط السلف", "Payroll approved and advance installments applied");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.Approve)) return Forbid();
        var run = await Scope.Filter(Db.PayrollRuns).FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound();
        if (run.Status != PayrollStatuses.Approved) return RedirectToAction(nameof(Details), new { id });
        run.Status = PayrollStatuses.Paid;
        run.PaidAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
        await Activity.LogAsync("PayrollRun", id.ToString(), "Paid", companyId: run.CompanyId, details: run.PeriodLabel);
        Flash("تم تسجيل صرف الرواتب", "Payroll marked as paid");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.Delete)) return Forbid();
        var run = await Scope.Filter(Db.PayrollRuns).FirstOrDefaultAsync(r => r.Id == id);
        if (run == null) return NotFound();
        if (run.Status != PayrollStatuses.Draft)
        {
            Flash("لا يمكن حذف مسير معتمد", "Approved payroll runs cannot be deleted", true);
            return RedirectToAction(nameof(Details), new { id });
        }
        Db.PayrollRuns.Remove(run);
        await Db.SaveChangesAsync();
        await Activity.LogAsync("PayrollRun", id.ToString(), "Delete", companyId: run.CompanyId);
        Flash("تم حذف المسودة", "Draft deleted");
        return RedirectToAction(nameof(Index), new { year = run.Year });
    }

    [HttpGet]
    public async Task<IActionResult> Payslips(int id, int? employeeId = null)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var run = await LoadRunAsync(id, tracking: false);
        if (run == null) return NotFound();
        var items = run.Items.Where(i => employeeId == null || i.EmployeeId == employeeId)
            .Where(i => Scope.Current.BranchId == null || i.BranchId == Scope.Current.BranchId)
            .OrderBy(i => i.Employee!.NameAr).ToList();
        return View(new PayslipVM { Run = run, Items = items, BranchNames = await BranchNamesAsync(run.CompanyId) });
    }

    /// <summary>Bank transfer sheet (CSV, UTF-8 BOM so Excel opens Arabic correctly).</summary>
    [HttpGet]
    public async Task<IActionResult> BankFile(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var run = await LoadRunAsync(id, tracking: false);
        if (run == null) return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("الرقم الوظيفي,اسم الموظف,البنك,الآيبان,اسم المستفيد,صافي الراتب");
        foreach (var i in run.Items.OrderBy(i => i.Employee!.NameAr))
        {
            var e = i.Employee!;
            var f = e.Financial;
            sb.AppendLine(string.Join(",", Csv(e.EmployeeCode), Csv(e.NameAr), Csv(f?.BankName), Csv(f?.Iban),
                Csv(f?.BeneficiaryName ?? e.NameAr), i.Net.ToString("0.00", CultureInfo.InvariantCulture)));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"payroll-{run.Company?.Slug ?? run.CompanyId.ToString()}-{run.Year}-{run.Month:00}.csv");
    }

    private static string Csv(string? v) => v == null ? "" : (v.Contains(',') || v.Contains('"') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v);

    private async Task<PayrollRun?> LoadRunAsync(int id, bool tracking)
    {
        var q = Scope.Filter(tracking ? Db.PayrollRuns : Db.PayrollRuns.AsNoTracking())
            .Include(r => r.Company)
            .Include(r => r.Items).ThenInclude(i => i.Employee).ThenInclude(e => e!.Financial);
        return await q.FirstOrDefaultAsync(r => r.Id == id);
    }

    private Task<Dictionary<int, string>> BranchNamesAsync(int companyId) =>
        Db.Branches.AsNoTracking().Where(b => b.CompanyId == companyId).ToDictionaryAsync(b => b.Id, b => b.NameAr);
}

public class PayrollAdjustment
{
    public int ItemId { get; set; }
    public decimal Additions { get; set; }
    public decimal OtherDeductions { get; set; }
    public string? Notes { get; set; }
}
