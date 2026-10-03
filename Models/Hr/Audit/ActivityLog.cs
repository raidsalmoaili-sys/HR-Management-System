using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr.Audit;

public class ActivityLog
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string UserId { get; set; } = "";
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public int? EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    [MaxLength(80)]  public string EntityType { get; set; } = "";
    [MaxLength(80)]  public string EntityId { get; set; } = "";
    [MaxLength(40)]  public string Action { get; set; } = "";

    public string? Details { get; set; }

    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }
}
