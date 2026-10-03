using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr.Support;

public enum TicketPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3,
}

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3,
}

public class SupportTicket
{
    public int Id { get; set; }

    [MaxLength(32)]
    public string TicketCode { get; set; } = "";

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = "";
    [ForeignKey(nameof(CreatedByUserId))]
    public ApplicationUser? CreatedByUser { get; set; }

    [MaxLength(450)]
    public string? AssignedToUserId { get; set; }
    [ForeignKey(nameof(AssignedToUserId))]
    public ApplicationUser? AssignedToUser { get; set; }

    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }

    [MaxLength(200)] public string Subject { get; set; } = "";
    public string Description { get; set; } = "";

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
