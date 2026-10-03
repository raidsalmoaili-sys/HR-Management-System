using System.ComponentModel.DataAnnotations;
using Project_HR_1.Models.Hr.Support;

namespace Project_HR_1.Models.ViewModels.Help;

public class CreateTicketVM
{
    [Required, StringLength(200)]
    public string Subject { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
}

public class MyTicketsVM
{
    public IReadOnlyList<SupportTicket> Tickets { get; set; } = Array.Empty<SupportTicket>();
}
