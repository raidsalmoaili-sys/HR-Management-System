using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Modules;
using Project_HR_1.Models.ViewModels.Modules;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.HrModules;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

/// <summary>لوحة المعلومات — headcount, Saudization, attendance, payroll trend and expiry alerts for the current scope.</summary>
public class HrDashboardController : HrModuleControllerBase
{
    private const int AlertDays = 60;
    protected override string ServiceId => "dashboard";

    public HrDashboardController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
        : base(db, perms, scope, activity) { }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var yearStart = new DateOnly(today.Year, 1, 1);
        var alertLimit = today.AddDays(AlertDays);

        var all = await Scope.Filter(Db.Employees.AsNoTracking())
            .Include(e => e.Company).Include(e => e.Department).Include(e => e.Financial)
            .ToListAsync();
        var working = all.Where(e => e.Status is EmployeeStatuses.Active or EmployeeStatuses.Leave).ToList();
        bool IsSaudi(Employee e) => (e.NationalityCode ?? "SA") == "SA";

        var vm = new HrDashboardVM
        {
            TotalEmployees = all.Count,
            ActiveEmployees = working.Count(e => e.Status == EmployeeStatuses.Active),
            OnLeaveNow = working.Count(e => e.Status == EmployeeStatuses.Leave),
            SaudiCount = working.Count(IsSaudi),
            NewHiresThisYear = all.Count(e => e.HireDate >= yearStart),
            LeftThisYear = all.Count(e => EmployeeStatuses.EndsWork(e.Status) && e.LastWorkDate >= yearStart),
            MonthlySalaryCost = working.Sum(e => e.BasicSalary
                + (e.Financial?.HousingAllowance ?? 0) + (e.Financial?.TransportAllowance ?? 0) + (e.Financial?.OtherAllowance ?? 0)),
        };
        vm.SaudizationRate = working.Count == 0 ? 0 : Math.Round(vm.SaudiCount * 100.0 / working.Count, 1);

        // ── Attendance today + last 7 days ──
        var weekStart = today.AddDays(-6);
        var att = await Scope.Filter(Db.AttendanceRecords.AsNoTracking())
            .Where(a => a.Date >= weekStart && a.Date <= today)
            .Select(a => new { a.Date, a.Status }).ToListAsync();
        var todays = att.Where(a => a.Date == today).ToList();
        vm.RecordedToday = todays.Count;
        vm.PresentToday = todays.Count(a => AttendanceStatuses.CountsAsWorked(a.Status));
        vm.LateToday = todays.Count(a => a.Status == AttendanceStatuses.Late);
        vm.AbsentToday = todays.Count(a => a.Status == AttendanceStatuses.Absent);
        var expectedToday = todays.Count(a => a.Status != AttendanceStatuses.Holiday && a.Status != AttendanceStatuses.Leave);
        vm.AttendanceRateToday = expectedToday == 0 ? null : Math.Round(vm.PresentToday * 100.0 / expectedToday, 1);

        for (var d = weekStart; d <= today; d = d.AddDays(1))
        {
            var day = att.Where(a => a.Date == d).ToList();
            var expected = day.Count(a => a.Status != AttendanceStatuses.Holiday && a.Status != AttendanceStatuses.Leave);
            var rate = expected == 0 ? 0 : Math.Round(day.Count(a => AttendanceStatuses.CountsAsWorked(a.Status)) * 100m / expected, 1);
            vm.Attendance7Days.Add(new LabelValue(d.ToString("dd/MM"), d.ToString("dd/MM"), rate,
                HrCalendar.IsWeekend(d) ? "#cbd5e1" : null));
        }

        // ── Breakdowns ──
        vm.ByCompany = working.GroupBy(e => e.Company)
            .Select(g => new LabelValue(g.Key?.ShortNameAr ?? g.Key?.NameAr ?? "—", g.Key?.ShortNameEn ?? g.Key?.NameEn ?? "—", g.Count()))
            .OrderByDescending(x => x.Value).ToList();

        vm.ByNationality = working
            .GroupBy(e => IsSaudi(e) ? "SA" : e.NationalityCode!)
            .Select(g =>
            {
                var f = g.First();
                return IsSaudi(f)
                    ? new LabelValue("سعودي", "Saudi", g.Count(), "#16a34a")
                    : new LabelValue(f.NationalityAr ?? g.Key, f.NationalityEn ?? g.Key, g.Count());
            })
            .OrderByDescending(x => x.Value).Take(6).ToList();

        vm.ByDepartment = working
            .GroupBy(e => e.Department?.NameAr ?? e.DeptAr ?? "غير محدد")
            .Select(g => new LabelValue(g.Key, g.First().Department?.NameEn ?? g.Key, g.Count()))
            .OrderByDescending(x => x.Value).Take(8).ToList();

        // ── Leaves ──
        var leaves = Scope.Filter(Db.LeaveRequests.AsNoTracking());
        vm.PendingLeaves = await leaves.CountAsync(l => l.Status == RequestStatuses.Pending);
        var leaveYear = await leaves.Where(l => l.Status == RequestStatuses.Approved && l.StartDate >= yearStart)
            .GroupBy(l => l.LeaveType).Select(g => new { g.Key, Days = g.Sum(x => x.Days) }).ToListAsync();
        vm.LeaveByType = leaveYear.Select(x =>
        {
            var t = LeaveTypes.Get(x.Key);
            return new LabelValue(t.Ar, t.En, x.Days, t.Color);
        }).OrderByDescending(x => x.Value).ToList();
        vm.LatestLeaves = await leaves.Include(l => l.Employee)
            .OrderBy(l => l.Status == RequestStatuses.Pending ? 0 : 1).ThenByDescending(l => l.CreatedAt)
            .Take(6).ToListAsync();

        // ── Advances ──
        var advances = await Scope.Filter(Db.SalaryAdvances.AsNoTracking())
            .Where(a => a.Status == RequestStatuses.Pending || a.Status == RequestStatuses.Approved).ToListAsync();
        vm.PendingAdvances = advances.Count(a => a.Status == RequestStatuses.Pending);
        vm.AdvancesOutstanding = advances.Where(a => a.Status == RequestStatuses.Approved).Sum(a => a.Remaining);

        // ── Payroll trend (last 6 periods, approved/paid) ──
        var runs = await Scope.Filter(Db.PayrollRuns.AsNoTracking())
            .Where(r => r.Status != PayrollStatuses.Draft)
            .Select(r => new { r.Year, r.Month, r.TotalNet }).ToListAsync();
        vm.PayrollTrend = runs.GroupBy(r => (r.Year, r.Month))
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month).Take(6)
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new LabelValue(HrCalendar.MonthNameAr(g.Key.Month), HrCalendar.MonthNameEn(g.Key.Month)[..3], g.Sum(x => x.TotalNet)))
            .ToList();
        vm.LastPayroll = await Scope.Filter(Db.PayrollRuns.AsNoTracking()).Include(r => r.Company)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).FirstOrDefaultAsync();

        // ── Expiry alerts: IDs, iqamas, passports, contracts ──
        foreach (var e in working)
        {
            void Add(DateOnly? d, string ar, string en)
            {
                if (d.HasValue && d.Value <= alertLimit)
                    vm.Expiring.Add((e, ar, en, d.Value, d.Value.DayNumber - today.DayNumber));
            }
            Add(e.IqamaExpiry, "الإقامة", "Iqama");
            Add(e.NationalIdExpiry, "الهوية الوطنية", "National ID");
            Add(e.PassportExpiry, "جواز السفر", "Passport");
        }
        vm.DocsExpiring60 = vm.Expiring.Count;

        var contracts = await Scope.Filter(Db.EmploymentContracts.AsNoTracking()).Include(c => c.Employee)
            .Where(c => c.Status == ContractStatuses.Active && c.EndDate != null && c.EndDate <= alertLimit)
            .ToListAsync();
        vm.ContractsExpiring60 = contracts.Count;
        foreach (var c in contracts.Where(c => c.Employee != null))
            vm.Expiring.Add((c.Employee!, "عقد العمل", "Contract", c.EndDate!.Value, c.EndDate.Value.DayNumber - today.DayNumber));

        vm.Expiring = vm.Expiring.OrderBy(x => x.DaysLeft).Take(12).ToList();
        return View(vm);
    }
}
