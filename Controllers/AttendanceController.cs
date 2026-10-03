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

/// <summary>التحضير — daily attendance sheet + monthly summary. See docs/features/hr-core-modules.md</summary>
public class AttendanceController : HrModuleControllerBase
{
    /// <summary>Official start of the working day; check-ins after start + grace are "late".</summary>
    public static readonly TimeOnly WorkStart = new(8, 0);
    public const int GraceMinutes = 15;

    protected override string ServiceId => "attendance";

    public AttendanceController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
        : base(db, perms, scope, activity) { }

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date = null, int? branchId = null, string? q = null)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var day = date ?? DateOnly.FromDateTime(DateTime.Today);

        var empQ = ScopedWorkingEmployees().Include(e => e.Branch).AsQueryable();
        if (branchId.HasValue) empQ = empQ.Where(e => e.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            empQ = empQ.Where(e => e.NameAr.Contains(t) || e.NameEn.Contains(t) || e.EmployeeCode.Contains(t));
        }
        var employees = await empQ.OrderBy(e => e.BranchId).ThenBy(e => e.NameAr).ToListAsync();
        var ids = employees.Select(e => e.Id).ToList();

        var records = await Db.AttendanceRecords.AsNoTracking()
            .Where(a => a.Date == day && ids.Contains(a.EmployeeId))
            .ToDictionaryAsync(a => a.EmployeeId);
        var onLeave = await Db.LeaveRequests.AsNoTracking()
            .Where(l => ids.Contains(l.EmployeeId) && l.Status == RequestStatuses.Approved && l.StartDate <= day && l.EndDate >= day)
            .Select(l => l.EmployeeId).Distinct().ToListAsync();

        var rows = employees.Select(e =>
        {
            records.TryGetValue(e.Id, out var r);
            return new AttendanceRowVM
            {
                Employee = e,
                Record = r,
                SuggestedStatus = r?.Status ?? (onLeave.Contains(e.Id) ? AttendanceStatuses.Leave
                                              : HrCalendar.IsWeekend(day) ? AttendanceStatuses.Holiday : AttendanceStatuses.Present),
            };
        }).ToList();

        var perms = await PermissionsAsync();
        return View(new AttendanceDayVM
        {
            Date = day,
            BranchId = branchId,
            Search = q,
            Branches = await ScopedBranchesAsync(),
            Rows = rows,
            IsWeekend = HrCalendar.IsWeekend(day),
            RecordedCount = records.Count,
            CanEdit = perms.CanCreate || perms.CanEdit,
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDay(DateOnly date, int? branchId, List<AttendanceEntryVM> entries)
    {
        if (!await CanAsync(HrBusinessPermissions.Create) && !await CanAsync(HrBusinessPermissions.Edit)) return Forbid();

        var ids = entries.Select(e => e.EmployeeId).ToList();
        var employees = await Scope.Filter(Db.Employees.AsNoTracking())
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.CompanyId, e.BranchId })
            .ToDictionaryAsync(e => e.Id);
        var existing = await Db.AttendanceRecords
            .Where(a => a.Date == date && ids.Contains(a.EmployeeId))
            .ToDictionaryAsync(a => a.EmployeeId);

        var saved = 0;
        foreach (var entry in entries)
        {
            if (!employees.TryGetValue(entry.EmployeeId, out var emp)) continue;   // outside scope → ignore
            if (string.IsNullOrEmpty(entry.Status) || AttendanceStatuses.All.All(s => s.Code != entry.Status)) continue;

            if (!existing.TryGetValue(entry.EmployeeId, out var rec))
            {
                rec = new AttendanceRecord { EmployeeId = emp.Id, CompanyId = emp.CompanyId, BranchId = emp.BranchId, Date = date };
                Db.AttendanceRecords.Add(rec);
            }

            var worked = AttendanceStatuses.CountsAsWorked(entry.Status);
            rec.CheckIn = worked ? entry.CheckIn : null;
            rec.CheckOut = worked ? entry.CheckOut : null;
            rec.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? null : entry.Notes.Trim();
            rec.LateMinutes = 0;
            rec.Status = entry.Status;
            if (rec.CheckIn.HasValue && entry.Status != AttendanceStatuses.Remote)
            {
                var late = (int)(rec.CheckIn.Value - WorkStart).TotalMinutes;
                if (late > GraceMinutes)
                {
                    rec.LateMinutes = late;
                    rec.Status = AttendanceStatuses.Late;
                }
                else if (rec.Status == AttendanceStatuses.Late)
                {
                    rec.Status = AttendanceStatuses.Present;
                }
            }
            rec.RecordedByUserId = CurrentUserId;
            rec.UpdatedAt = DateTime.UtcNow;
            saved++;
        }

        await Db.SaveChangesAsync();
        await Activity.LogAsync("Attendance", date.ToString("yyyy-MM-dd"), "SaveDay", details: $"{saved} records", companyId: Scope.Current.CompanyId, branchId: branchId);
        Flash($"تم حفظ تحضير {saved} موظف ليوم {date:yyyy/MM/dd}", $"Saved attendance for {saved} employees on {date:yyyy-MM-dd}");
        return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd"), branchId });
    }

    [HttpGet]
    public async Task<IActionResult> Monthly(int? year = null, int? month = null, int? branchId = null)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = Math.Clamp(month ?? today.Month, 1, 12);
        var start = PayrollCalculator.MonthStart(y, m);
        var end = PayrollCalculator.MonthEnd(y, m);

        var empQ = ScopedWorkingEmployees().AsQueryable();
        if (branchId.HasValue) empQ = empQ.Where(e => e.BranchId == branchId.Value);
        var employees = await empQ.OrderBy(e => e.NameAr).ToListAsync();
        var ids = employees.Select(e => e.Id).ToList();

        var records = await Db.AttendanceRecords.AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId) && a.Date >= start && a.Date <= end)
            .ToListAsync();
        var byEmp = records.GroupBy(r => r.EmployeeId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Date.Day, r => r.Status));

        var workingDays = HrCalendar.WorkingDays(start, end < DateOnly.FromDateTime(today) ? end : DateOnly.FromDateTime(today));
        var rows = employees.Select(e =>
        {
            var days = byEmp.GetValueOrDefault(e.Id) ?? new Dictionary<int, string>();
            var worked = days.Values.Count(AttendanceStatuses.CountsAsWorked);
            var counted = days.Values.Count(s => s != AttendanceStatuses.Holiday);
            return new AttendanceMonthRowVM
            {
                Employee = e,
                Days = days,
                Present = days.Values.Count(s => s == AttendanceStatuses.Present || s == AttendanceStatuses.Remote),
                Late = days.Values.Count(s => s == AttendanceStatuses.Late),
                Absent = days.Values.Count(s => s == AttendanceStatuses.Absent),
                Leave = days.Values.Count(s => s == AttendanceStatuses.Leave),
                LateMinutes = records.Where(r => r.EmployeeId == e.Id).Sum(r => r.LateMinutes),
                Rate = counted == 0 ? null : Math.Round(worked * 100.0 / counted, 0),
            };
        }).ToList();

        return View(new AttendanceMonthVM
        {
            Year = y, Month = m, BranchId = branchId,
            DaysInMonth = end.Day,
            Branches = await ScopedBranchesAsync(),
            Rows = rows,
            WorkingDaysSoFar = workingDays,
        });
    }
}
