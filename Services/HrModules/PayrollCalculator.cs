using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.Modules;

namespace Project_HR_1.Services.HrModules;

/// <summary>
/// Saudi payroll rules used by the payroll run. Values are constants on purpose (one place to review);
/// see docs/features/hr-core-modules.md before changing them.
/// </summary>
public static class PayrollRules
{
    /// <summary>GOSI employee share for Saudi nationals (annuities 9% + SANED 0.75%) of basic + housing.</summary>
    public const decimal GosiSaudiEmployeeRate = 0.0975m;
    /// <summary>Maximum contributory wage (basic + housing) for GOSI.</summary>
    public const decimal GosiMaxBase = 45000m;
    /// <summary>Daily rate = monthly gross / 30 (common practice in KSA for absence deduction).</summary>
    public const decimal DaysPerMonth = 30m;
}

public class PayrollCalculator
{
    private readonly AppDbContext _db;
    public PayrollCalculator(AppDbContext db) => _db = db;

    public static DateOnly MonthStart(int year, int month) => new(year, month, 1);
    public static DateOnly MonthEnd(int year, int month) => new(year, month, DateTime.DaysInMonth(year, month));

    /// <summary>
    /// (Re)builds the items of a DRAFT run from the current employee, attendance, leave and advance data.
    /// Manual fields (Additions / OtherDeductions / Notes) are kept for employees that were already in the run.
    /// </summary>
    public async Task BuildItemsAsync(PayrollRun run)
    {
        if (run.Status != PayrollStatuses.Draft)
            throw new InvalidOperationException("Only draft payroll runs can be recalculated.");

        var start = MonthStart(run.Year, run.Month);
        var end = MonthEnd(run.Year, run.Month);

        var employees = await _db.Employees
            .Include(e => e.Financial)
            .Where(e => e.CompanyId == run.CompanyId)
            .Where(e => e.HireDate == null || e.HireDate <= end)
            .Where(e => e.Status == EmployeeStatuses.Active || e.Status == EmployeeStatuses.Leave
                        || (e.LastWorkDate != null && e.LastWorkDate >= start))
            .ToListAsync();
        var ids = employees.Select(e => e.Id).ToList();

        var absences = await _db.AttendanceRecords
            .Where(a => ids.Contains(a.EmployeeId) && a.Date >= start && a.Date <= end && a.Status == AttendanceStatuses.Absent)
            .GroupBy(a => a.EmployeeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var unpaidLeaves = await _db.LeaveRequests
            .Where(l => ids.Contains(l.EmployeeId) && l.Status == RequestStatuses.Approved && l.LeaveType == LeaveTypes.Unpaid
                        && l.StartDate <= end && l.EndDate >= start)
            .ToListAsync();

        var advances = await _db.SalaryAdvances
            .Where(a => ids.Contains(a.EmployeeId) && a.Status == RequestStatuses.Approved && a.RepaidAmount < a.Amount
                        && (a.StartYear < run.Year || (a.StartYear == run.Year && a.StartMonth <= run.Month)))
            .ToListAsync();

        var existing = run.Items.ToDictionary(i => i.EmployeeId);
        var keep = new HashSet<int>();

        foreach (var e in employees)
        {
            var f = e.Financial;
            var basic = e.BasicSalary;
            if (f?.SalaryType == SalaryType.Daily && f.DailyRate is > 0)
                basic = Math.Round(f.DailyRate.Value * PayrollRules.DaysPerMonth, 2);
            var housing = f?.HousingAllowance ?? 0;
            var transport = f?.TransportAllowance ?? 0;
            var other = f?.OtherAllowance ?? 0;

            // Pro-rate joiners/leavers inside the month (30-day basis).
            var from = e.HireDate.HasValue && e.HireDate > start ? e.HireDate.Value : start;
            var to = e.LastWorkDate.HasValue && e.LastWorkDate < end ? e.LastWorkDate.Value : end;
            if (to < from) continue;
            var daysInMonth = end.Day;
            var workedDays = to.DayNumber - from.DayNumber + 1;
            var factor = workedDays >= daysInMonth ? 1m : Math.Round(workedDays / PayrollRules.DaysPerMonth, 4);
            if (factor < 1m)
            {
                basic = Math.Round(basic * factor, 2);
                housing = Math.Round(housing * factor, 2);
                transport = Math.Round(transport * factor, 2);
                other = Math.Round(other * factor, 2);
            }

            var dailyRate = (e.BasicSalary + (f?.HousingAllowance ?? 0) + (f?.TransportAllowance ?? 0) + (f?.OtherAllowance ?? 0)) / PayrollRules.DaysPerMonth;
            var absentDays = absences.GetValueOrDefault(e.Id);
            var unpaidDays = unpaidLeaves.Where(l => l.EmployeeId == e.Id)
                .Sum(l => Math.Max(0, Math.Min(l.EndDate.DayNumber, end.DayNumber) - Math.Max(l.StartDate.DayNumber, start.DayNumber) + 1));

            var gosi = 0m;
            if ((e.NationalityCode ?? "SA") == "SA")
                gosi = Math.Round(Math.Min(basic + housing, PayrollRules.GosiMaxBase) * PayrollRules.GosiSaudiEmployeeRate, 2);

            var advanceDeduction = advances.Where(a => a.EmployeeId == e.Id)
                .Sum(a => Math.Min(a.MonthlyInstallment, a.Remaining));

            if (!existing.TryGetValue(e.Id, out var item))
            {
                item = new PayrollItem { EmployeeId = e.Id };
                run.Items.Add(item);
            }
            keep.Add(e.Id);
            item.BranchId = e.BranchId;
            item.BasicSalary = basic;
            item.HousingAllowance = housing;
            item.TransportAllowance = transport;
            item.OtherAllowance = other;
            item.AbsentDays = absentDays;
            item.UnpaidLeaveDays = unpaidDays;
            item.AbsenceDeduction = Math.Round(dailyRate * (absentDays + unpaidDays), 2);
            item.GosiDeduction = gosi;
            item.AdvanceDeduction = advanceDeduction;
        }

        foreach (var stale in run.Items.Where(i => !keep.Contains(i.EmployeeId)).ToList())
        {
            run.Items.Remove(stale);
            if (stale.Id != 0) _db.PayrollItems.Remove(stale);
        }

        RecalcTotals(run);
    }

    public static void RecalcTotals(PayrollRun run)
    {
        run.EmployeesCount = run.Items.Count;
        run.TotalGross = run.Items.Sum(i => i.Gross);
        run.TotalDeductions = run.Items.Sum(i => i.TotalDeductions);
        run.TotalNet = run.Items.Sum(i => i.Net);
    }

    /// <summary>Applies the run's advance deductions to the advances (called once, on approval).</summary>
    public async Task ApplyAdvanceRepaymentsAsync(PayrollRun run)
    {
        var ids = run.Items.Where(i => i.AdvanceDeduction > 0).Select(i => i.EmployeeId).ToList();
        if (ids.Count == 0) return;

        var advances = await _db.SalaryAdvances
            .Where(a => ids.Contains(a.EmployeeId) && a.Status == RequestStatuses.Approved && a.RepaidAmount < a.Amount
                        && (a.StartYear < run.Year || (a.StartYear == run.Year && a.StartMonth <= run.Month)))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        foreach (var item in run.Items.Where(i => i.AdvanceDeduction > 0))
        {
            var left = item.AdvanceDeduction;
            foreach (var a in advances.Where(a => a.EmployeeId == item.EmployeeId))
            {
                if (left <= 0) break;
                var pay = Math.Min(Math.Min(a.MonthlyInstallment, a.Remaining), left);
                a.RepaidAmount += pay;
                left -= pay;
                if (a.Remaining <= 0) a.Status = RequestStatuses.Closed;
            }
        }
    }
}

public static class HrCalendar
{
    /// <summary>Saudi weekend = Friday + Saturday.</summary>
    public static bool IsWeekend(DateOnly d) => d.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

    public static int WorkingDays(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (!IsWeekend(d)) n++;
        return n;
    }

    public static string MonthNameAr(int month) => month switch
    {
        1 => "يناير", 2 => "فبراير", 3 => "مارس", 4 => "أبريل", 5 => "مايو", 6 => "يونيو",
        7 => "يوليو", 8 => "أغسطس", 9 => "سبتمبر", 10 => "أكتوبر", 11 => "نوفمبر", 12 => "ديسمبر",
        _ => month.ToString(),
    };

    public static string MonthNameEn(int month) =>
        System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month);

    public static string Money(decimal v) => v.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture);
    public static string Money0(decimal v) => v.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture);
}
