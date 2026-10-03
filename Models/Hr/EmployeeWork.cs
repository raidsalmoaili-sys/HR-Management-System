using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr;

public enum WorkSystem { FullTime, PartTime, Remote }
public enum ContractType { Limited, Unlimited }

// 1:1 with Employee — EmployeeId is both PK and FK.
public class EmployeeWork
{
    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    // Job assignment
    public int? ActualJobTitleId { get; set; }
    [ForeignKey(nameof(ActualJobTitleId))] public JobTitle? ActualJobTitle { get; set; }
    public int? DirectSupervisorId { get; set; }
    [ForeignKey(nameof(DirectSupervisorId))] public Employee? DirectSupervisor { get; set; }
    [MaxLength(200)] public string? WorkLocation { get; set; }
    public WorkSystem WorkSystem { get; set; } = WorkSystem.FullTime;
    public decimal? DailyWorkHours { get; set; }

    // Contract (ContractStartDate/EndDate already live on Employee)
    public ContractType ContractType { get; set; } = ContractType.Unlimited;
    public int? ContractDurationMonths { get; set; }
    public int ProbationDays { get; set; } = 90;

    // Leave balance
    public int AnnualLeaveBalance { get; set; }
    public DateOnly? LastLeaveDate { get; set; }
    public int SickLeaveUsedDays { get; set; }
}
