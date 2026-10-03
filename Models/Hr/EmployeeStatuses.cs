namespace Project_HR_1.Models.Hr;

// Employee.Status values (the column already exists — plain string, max 32).
// See docs/features/employee-status.md
public record EmployeeStatusInfo(string Code, string NameAr, string NameEn, string Badge, string Icon);

public static class EmployeeStatuses
{
    public const string Active = "active";
    public const string Leave = "leave";
    public const string Resigned = "resigned";
    public const string Absconded = "absconded";

    public static readonly IReadOnlyList<EmployeeStatusInfo> All = new[]
    {
        new EmployeeStatusInfo(Active,    "على رأس العمل", "Active",    "success", "fa-briefcase"),
        new EmployeeStatusInfo(Leave,     "مجاز",          "On leave",  "warning", "fa-umbrella-beach"),
        new EmployeeStatusInfo(Resigned,  "مستقيل",        "Resigned",  "muted",   "fa-door-open"),
        new EmployeeStatusInfo(Absconded, "هارب",          "Absconded", "danger",  "fa-person-running"),
    };

    public static bool IsValid(string? code) => code != null && All.Any(s => s.Code == code);

    // Statuses that end the employee's work — the chosen date is stored as Employee.LastWorkDate.
    public static bool EndsWork(string? code) => code is Resigned or Absconded;

    // Unknown/legacy values still display (as themselves, grey) instead of breaking the page.
    public static EmployeeStatusInfo Get(string? code) =>
        All.FirstOrDefault(s => s.Code == code) ?? new EmployeeStatusInfo(code ?? "", code ?? "—", code ?? "—", "muted", "fa-circle-question");
}
