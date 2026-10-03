using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.Hr.Modules;

// Entities for the six business services built on top of the employee database:
// attendance · leaves · payroll · contracts · advances (dashboard reads all of them).
// Every record carries CompanyId/BranchId (copied from the employee when the record is created)
// so IScopeContext.Filter<T>() can scope it without extra joins. See docs/features/hr-core-modules.md

// ─── التحضير ─────────────────────────────────────────────────────────────
public static class AttendanceStatuses
{
    public const string Present = "present";
    public const string Late    = "late";
    public const string Absent  = "absent";
    public const string Leave   = "leave";
    public const string Remote  = "remote";
    public const string Holiday = "holiday";

    public static readonly IReadOnlyList<(string Code, string Ar, string En, string Badge)> All = new[]
    {
        (Present, "حاضر",        "Present", "success"),
        (Late,    "متأخر",       "Late",    "warning"),
        (Absent,  "غائب",        "Absent",  "danger"),
        (Leave,   "إجازة",       "Leave",   "info"),
        (Remote,  "عن بُعد",     "Remote",  "info"),
        (Holiday, "عطلة رسمية",  "Holiday", "muted"),
    };

    public static (string Code, string Ar, string En, string Badge) Get(string? code) =>
        All.FirstOrDefault(s => s.Code == code) is { Code: not null } s ? s : (code ?? "", code ?? "—", code ?? "—", "muted");

    /// <summary>Statuses that count as a worked day (for attendance rates).</summary>
    public static bool CountsAsWorked(string? code) => code is Present or Late or Remote;
}

public class AttendanceRecord : IScoped
{
    public long Id { get; set; }

    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }

    [MaxLength(16)] public string Status { get; set; } = AttendanceStatuses.Present;
    public int LateMinutes { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }

    [MaxLength(450)] public string? RecordedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public double? WorkedHours => CheckIn.HasValue && CheckOut.HasValue && CheckOut > CheckIn
        ? Math.Round((CheckOut.Value - CheckIn.Value).TotalHours, 1) : null;

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => BranchId;
}

// ─── الإجازات ────────────────────────────────────────────────────────────
public static class LeaveTypes
{
    public const string Annual    = "annual";
    public const string Sick      = "sick";
    public const string Emergency = "emergency";
    public const string Unpaid    = "unpaid";
    public const string Maternity = "maternity";
    public const string Marriage  = "marriage";
    public const string Bereavement = "bereavement";
    public const string Hajj      = "hajj";
    public const string Exam      = "exam";

    // Days = statutory entitlement under the Saudi Labor Law (informational; annual uses the employee balance).
    public static readonly IReadOnlyList<(string Code, string Ar, string En, string Color, int? Days)> All = new[]
    {
        (Annual,      "سنوية",           "Annual",      "#2563eb", (int?)null),
        (Sick,        "مرضية",           "Sick",        "#dc2626", (int?)120),
        (Emergency,   "اضطرارية",        "Emergency",   "#d97706", (int?)5),
        (Unpaid,      "بدون راتب",       "Unpaid",      "#64748b", (int?)null),
        (Maternity,   "وضع",             "Maternity",   "#db2777", (int?)84),
        (Marriage,    "زواج",            "Marriage",    "#7c3aed", (int?)5),
        (Bereavement, "وفاة قريب",       "Bereavement", "#334155", (int?)5),
        (Hajj,        "حج",              "Hajj",        "#059669", (int?)15),
        (Exam,        "اختبارات",        "Exams",       "#0891b2", (int?)null),
    };

    public static (string Code, string Ar, string En, string Color, int? Days) Get(string? code) =>
        All.FirstOrDefault(t => t.Code == code) is { Code: not null } t ? t : (code ?? "", code ?? "—", code ?? "—", "#64748b", null);

    /// <summary>Leave types that reduce pay in the payroll run.</summary>
    public static bool IsUnpaid(string? code) => code == Unpaid;
}

public static class RequestStatuses
{
    public const string Pending   = "pending";
    public const string Approved  = "approved";
    public const string Rejected  = "rejected";
    public const string Cancelled = "cancelled";
    public const string Closed    = "closed";   // advances: fully repaid

    public static (string Ar, string En, string Badge) Get(string? code) => code switch
    {
        Pending   => ("بانتظار الاعتماد", "Pending",   "warning"),
        Approved  => ("معتمد",            "Approved",  "success"),
        Rejected  => ("مرفوض",            "Rejected",  "danger"),
        Cancelled => ("ملغى",             "Cancelled", "muted"),
        Closed    => ("مسدد بالكامل",     "Repaid",    "info"),
        _         => (code ?? "—", code ?? "—", "muted"),
    };
}

public class LeaveRequest : IScoped
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    [MaxLength(16)] public string LeaveType { get; set; } = LeaveTypes.Annual;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }

    [MaxLength(16)] public string Status { get; set; } = RequestStatuses.Pending;
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(500)] public string? ReviewNote { get; set; }

    /// <summary>True once the days were taken from EmployeeWork.AnnualLeaveBalance (so cancel can give them back).</summary>
    public bool BalanceDeducted { get; set; }

    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => BranchId;
}

// ─── السلف ───────────────────────────────────────────────────────────────
public class SalaryAdvance : IScoped
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    [Column(TypeName = "numeric(12,2)")] public decimal Amount { get; set; }
    public int Installments { get; set; } = 1;
    [Column(TypeName = "numeric(12,2)")] public decimal MonthlyInstallment { get; set; }

    /// <summary>First payroll month the installment is deducted in.</summary>
    public int StartYear { get; set; }
    public int StartMonth { get; set; }

    [Column(TypeName = "numeric(12,2)")] public decimal RepaidAmount { get; set; }

    [MaxLength(500)] public string? Reason { get; set; }
    [MaxLength(16)] public string Status { get; set; } = RequestStatuses.Pending;

    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(500)] public string? ReviewNote { get; set; }

    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped] public decimal Remaining => Math.Max(0, Amount - RepaidAmount);

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => BranchId;
}

// ─── العقود ──────────────────────────────────────────────────────────────
public static class ContractStatuses
{
    public const string Draft      = "draft";
    public const string Active     = "active";
    public const string Renewed    = "renewed";
    public const string Terminated = "terminated";

    public static (string Ar, string En, string Badge) Get(string? code) => code switch
    {
        Draft      => ("مسودة",     "Draft",      "muted"),
        Active     => ("ساري",      "Active",     "success"),
        Renewed    => ("مُجدَّد",    "Renewed",    "info"),
        Terminated => ("منتهٍ/مُنهى", "Terminated", "danger"),
        _          => (code ?? "—", code ?? "—", "muted"),
    };
}

public class EmploymentContract : IScoped
{
    public int Id { get; set; }

    [MaxLength(32)] public string ContractNumber { get; set; } = "";

    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public ContractType ContractType { get; set; } = ContractType.Limited;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    [MaxLength(160)] public string? JobTitleAr { get; set; }

    [Column(TypeName = "numeric(12,2)")] public decimal BasicSalary { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal HousingAllowance { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal TransportAllowance { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal OtherAllowance { get; set; }

    public int ProbationDays { get; set; } = 90;
    public int AnnualLeaveDays { get; set; } = 21;
    public int WeeklyWorkDays { get; set; } = 5;
    public int DailyWorkHours { get; set; } = 8;
    public int NoticePeriodDays { get; set; } = 60;

    [MaxLength(16)] public string Status { get; set; } = ContractStatuses.Active;
    public int? RenewedFromId { get; set; }
    public DateOnly? TerminatedOn { get; set; }
    [MaxLength(500)] public string? TerminationReason { get; set; }
    [MaxLength(2000)] public string? ExtraTerms { get; set; }

    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped] public decimal TotalSalary => BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance;

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => BranchId;
}

// ─── مسيرات الرواتب ──────────────────────────────────────────────────────
public static class PayrollStatuses
{
    public const string Draft    = "draft";
    public const string Approved = "approved";
    public const string Paid     = "paid";

    public static (string Ar, string En, string Badge) Get(string? code) => code switch
    {
        Draft    => ("مسودة",   "Draft",    "warning"),
        Approved => ("معتمد",   "Approved", "info"),
        Paid     => ("مصروف",   "Paid",     "success"),
        _        => (code ?? "—", code ?? "—", "muted"),
    };
}

public class PayrollRun : IScoped
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))] public Company? Company { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    [MaxLength(16)] public string Status { get; set; } = PayrollStatuses.Draft;

    [Column(TypeName = "numeric(14,2)")] public decimal TotalGross { get; set; }
    [Column(TypeName = "numeric(14,2)")] public decimal TotalDeductions { get; set; }
    [Column(TypeName = "numeric(14,2)")] public decimal TotalNet { get; set; }
    public int EmployeesCount { get; set; }

    [MaxLength(500)] public string? Notes { get; set; }

    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    public List<PayrollItem> Items { get; set; } = new();

    [NotMapped] public string PeriodLabel => $"{Year}/{Month:00}";

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => null;
}

public class PayrollItem
{
    public int Id { get; set; }

    public int PayrollRunId { get; set; }
    [ForeignKey(nameof(PayrollRunId))] public PayrollRun? PayrollRun { get; set; }

    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    public int BranchId { get; set; }

    [Column(TypeName = "numeric(12,2)")] public decimal BasicSalary { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal HousingAllowance { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal TransportAllowance { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal OtherAllowance { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal Additions { get; set; }      // مكافآت/إضافي (manual)

    public int AbsentDays { get; set; }
    public int UnpaidLeaveDays { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal AbsenceDeduction { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal GosiDeduction { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal AdvanceDeduction { get; set; }
    [Column(TypeName = "numeric(12,2)")] public decimal OtherDeductions { get; set; } // manual

    [MaxLength(300)] public string? Notes { get; set; }

    [NotMapped] public decimal Gross => BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance + Additions;
    [NotMapped] public decimal TotalDeductions => AbsenceDeduction + GosiDeduction + AdvanceDeduction + OtherDeductions;
    [NotMapped] public decimal Net => Math.Max(0, Gross - TotalDeductions);
}
