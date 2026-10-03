using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.ViewModels.Management;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

[Authorize]
public class RoleAssignmentsController : Controller
{
    private const string Svc = "settings-role-assignments";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPermissionService _perms;
    private readonly IActivityLogger _activity;

    public RoleAssignmentsController(AppDbContext db, UserManager<ApplicationUser> users, IPermissionService perms, IActivityLogger activity)
    {
        _db = db;
        _users = users;
        _perms = perms;
        _activity = activity;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.View)) return Forbid();

        var assignments = await _db.EmployeeRoleAssignments.AsNoTracking()
            .Include(a => a.Employee)
            .Include(a => a.HrRole)
            .Include(a => a.Company)
            .Include(a => a.Branch)
            .OrderBy(a => a.Employee!.NameAr)
            .ToListAsync();

        var employees = await _db.Employees.AsNoTracking().OrderBy(e => e.NameAr).ToListAsync();
        var roles = await _db.HrRoles.AsNoTracking().OrderBy(r => r.NameAr).ToListAsync();
        var companies = await _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).ToListAsync();
        var branches = await _db.Branches.AsNoTracking().OrderBy(b => b.NameAr).ToListAsync();

        ViewData["Title"] = "Permissions";
        ViewData["CurrentServiceId"] = Svc;
        return View(new RoleAssignmentsListVM
        {
            Assignments = assignments,
            Employees = employees,
            Roles = roles,
            Companies = companies,
            Branches = branches,
            CanCreate = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create),
            CanDelete = await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAssignmentVM m)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Create)) return Forbid();
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));

        var uid = _users.GetUserId(User)!;
        var assignment = new EmployeeRoleAssignment
        {
            EmployeeId = m.EmployeeId,
            HrRoleId = m.HrRoleId,
            ScopeLevel = m.ScopeLevel,
            CompanyId  = m.ScopeLevel == ScopeLevel.Company ? m.CompanyId : null,
            BranchId   = m.ScopeLevel == ScopeLevel.Branch  ? m.BranchId  : null,
            AssignedByUserId = uid,
        };
        _db.EmployeeRoleAssignments.Add(assignment);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("EmployeeRoleAssignment", assignment.Id.ToString(), "Create",
            userId: uid, employeeId: m.EmployeeId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _perms.CanAsync(User, Svc, HrBusinessPermissions.Delete)) return Forbid();
        var a = await _db.EmployeeRoleAssignments.FirstOrDefaultAsync(x => x.Id == id);
        if (a == null) return NotFound();
        var empId = a.EmployeeId;
        _db.EmployeeRoleAssignments.Remove(a);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("EmployeeRoleAssignment", id.ToString(), "Delete",
            userId: _users.GetUserId(User), employeeId: empId);
        return RedirectToAction(nameof(Index));
    }
}
