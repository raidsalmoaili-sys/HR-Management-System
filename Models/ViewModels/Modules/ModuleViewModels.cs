using System.ComponentModel.DataAnnotations;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.Modules;

namespace Project_HR_1.Models.ViewModels.Modules;

// ─── Attendance ─────────────────────────────────────────────────────────
public class AttendanceRowVM
{
    public Employee Employee { get; set; } = null!;
    public AttendanceRecord? Record { get; set; }
    public string SuggestedStatus { get; set; } = AttendanceStatuses.Present;
}

public class AttendanceDayVM
{
    public DateOnly Date { get; set; }
    public int? BranchId { get; set; }
    public string? Search { get; set; }
    public List<Branch> Branches { get; set; } = new();
    public List<AttendanceRowVM> Rows { get; set; } = new();
    public bool IsWeekend { get; set; }
    public int RecordedCount { get; set; }
    public bool CanEdit { get; set; }

    public int Count(string status) => Rows.Count(r => r.Record?.Status == status);
}

public class AttendanceEntryVM
{
    public int EmployeeId { get; set; }
    public string? Status { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceMonthRowVM
{
    public Employee Employee { get; set; } = null!;
    public Dictionary<int, string> Days { get; set; } = new();
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int Leave { get; set; }
    public int LateMinutes { get; set; }
    public double? Rate { get; set; }
}

public class AttendanceMonthVM
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int? BranchId { get; set; }
    public int DaysInMonth { get; set; }
    public int WorkingDaysSoFar { get; set; }
    public List<Branch> Branches { get; set; } = new();
    public List<AttendanceMonthRowVM> Rows { get; set; } = new();
}

// ─── Shared employee picker item ────────────────────────────────────────
public record EmployeePick(int Id, string Code, string NameAr, string NameEn, string? BranchAr, int LeaveBalance, decimal BasicSalary);

// ─── Leaves ─────────────────────────────────────────────────────────────
public class LeavesListVM
{
    public List<LeaveRequest> Requests { get; set; } = new();
    public List<EmployeePick> Employees { get; set; } = new();
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Search { get; set; }
    public int PendingCount { get; set; }
    public int OnLeaveToday { get; set; }
    public int ApprovedThisMonthDays { get; set; }
    public int UpcomingCount { get; set; }
    public Controllers.ModulePermissions Perms { get; set; } = new();
}

public class LeaveCreateVM
{
    [Required] public int EmployeeId { get; set; }
    [Required, MaxLength(16)] public string LeaveType { get; set; } = LeaveTypes.Annual;
    [Required] public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [Required] public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [MaxLength(500)] public string? Reason { get; set; }
}

// ─── Advances ───────────────────────────────────────────────────────────
public class AdvancesListVM
{
    public List<SalaryAdvance> Advances { get; set; } = new();
    public List<EmployeePick> Employees { get; set; } = new();
    public string? Status { get; set; }
    public string? Search { get; set; }
    public decimal OutstandingTotal { get; set; }
    public int PendingCount { get; set; }
    public int ActiveCount { get; set; }
    public decimal RepaidTotal { get; set; }
    public Controllers.ModulePermissions Perms { get; set; } = new();
}

public class AdvanceCreateVM
{
    [Required] public int EmployeeId { get; set; }
    [Range(100, 1_000_000)] public decimal Amount { get; set; }
    [Range(1, 24)] public int Installments { get; set; } = 3;
    [Range(2000, 2100)] public int StartYear { get; set; } = DateTime.Today.Year;
    [Range(1, 12)] public int StartMonth { get; set; } = DateTime.Today.Month;
    [MaxLength(500)] public string? Reason { get; set; }
}

// ─── Contracts ──────────────────────────────────────────────────────────
public class ContractsListVM
{
    public List<EmploymentContract> Contracts { get; set; } = new();
    public List<EmployeePick> Employees { get; set; } = new();
    public string? Status { get; set; }
    public string? Search { get; set; }
    public bool ExpiringOnly { get; set; }
    public int ActiveCount { get; set; }
    public int Expiring60 { get; set; }
    public int Expired { get; set; }
    public int WithoutContract { get; set; }
    public Controllers.ModulePermissions Perms { get; set; } = new();
}

public class ContractFormVM
{
    public int? Id { get; set; }
    public int? RenewFromId { get; set; }
    [Required] public int EmployeeId { get; set; }
    public ContractType ContractType { get; set; } = ContractType.Limited;
    [Required] public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today).AddYears(1).AddDays(-1);
    [MaxLength(160)] public string? JobTitleAr { get; set; }
    [Range(0, 1_000_000)] public decimal BasicSalary { get; set; }
    [Range(0, 1_000_000)] public decimal HousingAllowance { get; set; }
    [Range(0, 1_000_000)] public decimal TransportAllowance { get; set; }
    [Range(0, 1_000_000)] public decimal OtherAllowance { get; set; }
    [Range(0, 180)] public int ProbationDays { get; set; } = 90;
    [Range(21, 60)] public int AnnualLeaveDays { get; set; } = 21;
    [Range(1, 7)] public int WeeklyWorkDays { get; set; } = 5;
    [Range(1, 12)] public int DailyWorkHours { get; set; } = 8;
    [Range(0, 180)] public int NoticePeriodDays { get; set; } = 60;
    [MaxLength(2000)] public string? ExtraTerms { get; set; }
    public bool SyncEmployee { get; set; } = true;

    public List<EmployeePick> Employees { get; set; } = new();
    public Employee? SelectedEmployee { get; set; }
}

public class ContractPrintVM
{
    public EmploymentContract Contract { get; set; } = null!;
    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
}

// ─── Payroll ────────────────────────────────────────────────────────────
public class PayrollListVM
{
    public List<PayrollRun> Runs { get; set; } = new();
    public List<Company> Companies { get; set; } = new();
    public int Year { get; set; }
    public decimal YearNet { get; set; }
    public decimal LastNet { get; set; }
    public int DraftCount { get; set; }
    public Controllers.ModulePermissions Perms { get; set; } = new();
}

public class PayrollRunVM
{
    public PayrollRun Run { get; set; } = null!;
    public List<PayrollItem> Items { get; set; } = new();
    public Dictionary<int, string> BranchNames { get; set; } = new();
    public int? BranchId { get; set; }
    public Controllers.ModulePermissions Perms { get; set; } = new();
}

public class PayslipVM
{
    public PayrollRun Run { get; set; } = null!;
    public List<PayrollItem> Items { get; set; } = new();
    public Dictionary<int, string> BranchNames { get; set; } = new();
}

// ─── Dashboard ──────────────────────────────────────────────────────────
public record LabelValue(string LabelAr, string LabelEn, decimal Value, string? Color = null);

public class HrDashboardVM
{
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int OnLeaveNow { get; set; }
    public int SaudiCount { get; set; }
    public double SaudizationRate { get; set; }
    public int NewHiresThisYear { get; set; }
    public int LeftThisYear { get; set; }

    public double? AttendanceRateToday { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int LateToday { get; set; }
    public int RecordedToday { get; set; }

    public int PendingLeaves { get; set; }
    public int PendingAdvances { get; set; }
    public decimal AdvancesOutstanding { get; set; }
    public int ContractsExpiring60 { get; set; }
    public int DocsExpiring60 { get; set; }

    public decimal MonthlySalaryCost { get; set; }
    public PayrollRun? LastPayroll { get; set; }

    public List<LabelValue> ByCompany { get; set; } = new();
    public List<LabelValue> ByNationality { get; set; } = new();
    public List<LabelValue> ByDepartment { get; set; } = new();
    public List<LabelValue> Attendance7Days { get; set; } = new();
    public List<LabelValue> PayrollTrend { get; set; } = new();
    public List<LabelValue> LeaveByType { get; set; } = new();

    public List<LeaveRequest> LatestLeaves { get; set; } = new();
    public List<(Employee Employee, string DocAr, string DocEn, DateOnly Expiry, int DaysLeft)> Expiring { get; set; } = new();
}
