namespace Project_HR_1.Services.Scope;

public class HrScope
{
    public ScopeLevel Level { get; set; } = ScopeLevel.Global;
    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }

    public static HrScope Global() => new() { Level = ScopeLevel.Global };
}
