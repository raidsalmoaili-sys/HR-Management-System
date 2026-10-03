using Project_HR_1.Models.Hr;

namespace Project_HR_1.Models.ViewModels.Entry;

public class CompanyStepVM
{
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
}

public class BranchStepVM
{
    public Company? Company { get; set; }
    public IReadOnlyList<Branch> Branches { get; set; } = Array.Empty<Branch>();
}

public class LauncherVM
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public IReadOnlyList<HrService> Services { get; set; } = Array.Empty<HrService>();
    public IReadOnlySet<string> AllowedServiceIds { get; set; } = new HashSet<string>();
    public IReadOnlyList<HrServiceCategory> Categories { get; set; } = Array.Empty<HrServiceCategory>();
    public Project_HR_1.Services.Scope.HrScope Scope { get; set; } = Project_HR_1.Services.Scope.HrScope.Global();
    public Company? ScopeCompany { get; set; }
    public Branch?  ScopeBranch  { get; set; }

    // ── Dashboard data ──────────────────────────────────────────────────
    public DashboardStats Stats { get; set; } = new();
    public IReadOnlyList<DocExpiryAlert> ExpiryAlerts { get; set; } = Array.Empty<DocExpiryAlert>();
    public IReadOnlyList<NationalityCount> NationalityBreakdown { get; set; } = Array.Empty<NationalityCount>();
    public IReadOnlyList<DeptCount> DeptBreakdown { get; set; } = Array.Empty<DeptCount>();
    public IReadOnlyList<DataIssueAlert> DataIssues { get; set; } = Array.Empty<DataIssueAlert>();
}

// Employee records that need a manual fix (see docs/features/employee-sheet-import.md).
// Computed live on every dashboard load, so an alert disappears as soon as the record is fixed.
public class DataIssueAlert
{
    public int    EmployeeId   { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string NameAr       { get; set; } = "";
    public string NameEn       { get; set; } = "";
    public string Kind         { get; set; } = "";   // "no-id" | "no-branch" | "id-mismatch"
    public string Note         { get; set; } = "";
}

public class DashboardStats
{
    public int TotalEmployees    { get; set; }
    public int SaudiCount        { get; set; }
    public int NonSaudiCount     { get; set; }
    public int AttendanceToday   { get; set; }
    public int ActiveLeaves      { get; set; }
    public int PendingAlerts     { get; set; }
}

public class DocExpiryAlert
{
    public int    EmployeeId   { get; set; }
    public string NameAr       { get; set; } = "";
    public string NameEn       { get; set; } = "";
    public string DocType      { get; set; } = "";   // "iqama" | "national-id" | "passport" | "contract"
    public DateOnly ExpiryDate { get; set; }
    public int    DaysLeft     { get; set; }
    public string Urgency      { get; set; } = "";   // "critical"(<14) | "warning"(<30) | "soon"(<90)
}

public class NationalityCount
{
    public string Label { get; set; } = "";
    public int    Count { get; set; }
}

public class DeptCount
{
    public string Label { get; set; } = "";
    public int    Count { get; set; }
}
