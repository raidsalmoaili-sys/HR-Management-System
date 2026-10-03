using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr.Audit;

public class LoginLog
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [MaxLength(256)] public string Email { get; set; } = "";

    public bool Success { get; set; }

    [MaxLength(64)]  public string? IpAddress { get; set; }
    [MaxLength(500)] public string? UserAgent { get; set; }
    [MaxLength(200)] public string? FailureReason { get; set; }
}
