using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.Hr.HrPermissions;

public class EmployeeRoleAssignment
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    public int HrRoleId { get; set; }
    [ForeignKey(nameof(HrRoleId))]
    public HrRole? HrRole { get; set; }

    public ScopeLevel ScopeLevel { get; set; }

    public int? CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))]
    public Company? Company { get; set; }

    public int? BranchId { get; set; }
    [ForeignKey(nameof(BranchId))]
    public Branch? Branch { get; set; }

    [MaxLength(450)]
    public string AssignedByUserId { get; set; } = "";

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
