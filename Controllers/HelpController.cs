using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.Support;
using Project_HR_1.Models.ViewModels.Help;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

[Authorize]
public class HelpController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IActivityLogger _activity;

    public HelpController(AppDbContext db, UserManager<ApplicationUser> users, IActivityLogger activity)
    {
        _db = db;
        _users = users;
        _activity = activity;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var uid = _users.GetUserId(User);
        var tickets = await _db.SupportTickets
            .AsNoTracking()
            .Where(t => t.CreatedByUserId == uid)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
        return View(new MyTicketsVM { Tickets = tickets });
    }

    [HttpGet]
    public IActionResult New() => View(new CreateTicketVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(CreateTicketVM m)
    {
        if (!ModelState.IsValid) return View(m);

        var uid = _users.GetUserId(User)!;
        var scope = ScopeCookieMiddleware.ReadFromCookie(HttpContext) ?? HrScope.Global();

        var ticket = new SupportTicket
        {
            TicketCode = $"T-{Random.Shared.Next(1000, 99999)}",
            CreatedByUserId = uid,
            CompanyId = scope.CompanyId,
            BranchId = scope.BranchId,
            Subject = m.Subject,
            Description = m.Description,
            Priority = m.Priority,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        await _activity.LogAsync("SupportTicket", ticket.Id.ToString(), "Create",
            userId: uid, companyId: scope.CompanyId, branchId: scope.BranchId);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Ticket(int id)
    {
        var ticket = await _db.SupportTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        var uid = _users.GetUserId(User);
        var isOwnerOfTicket = ticket.CreatedByUserId == uid;
        var isSuperAdmin = User.IsInRole(IdentityRoles.SuperAdmin);
        if (!isOwnerOfTicket && !isSuperAdmin) return Forbid();

        return View(ticket);
    }
}
