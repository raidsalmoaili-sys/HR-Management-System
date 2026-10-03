using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Support;
using Project_HR_1.Models.ViewModels.Management;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private const string Svc = "manage-tickets";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPermissionService _perms;
    private readonly IScopeContext _scope;
    private readonly IActivityLogger _activity;

    public TicketsController(AppDbContext db, UserManager<ApplicationUser> users, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
    {
        _db = db;
        _users = users;
        _perms = perms;
        _scope = scope;
        _activity = activity;
    }

    [HttpGet]
    public async Task<IActionResult> Index(TicketStatus? status = null)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.View, _scope.Current)) return Forbid();
        var q = _db.SupportTickets.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        var tickets = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
        ViewData["Title"] = "Support Tickets";
        ViewData["CurrentServiceId"] = Svc;
        return View(new TicketsListVM
        {
            Tickets = tickets,
            StatusFilter = status,
            CanManage = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current),
        });
    }

    [HttpGet("Tickets/Detail/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.View, _scope.Current)) return Forbid();
        var t = await _db.SupportTickets.AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Include(x => x.AssignedToUser)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        ViewData["Title"] = t.TicketCode;
        return View(new TicketDetailVM
        {
            Ticket = t,
            CanManage = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, TicketStatus status)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        var t = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        t.Status = status;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _activity.LogAsync("SupportTicket", id.ToString(), "ChangeStatus", details: status.ToString());
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, string? userId)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        var t = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        t.AssignedToUserId = string.IsNullOrEmpty(userId) ? null : userId;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _activity.LogAsync("SupportTicket", id.ToString(), "Assign", details: userId);
        return RedirectToAction(nameof(Detail), new { id });
    }
}
