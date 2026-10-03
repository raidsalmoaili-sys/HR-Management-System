using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

[Authorize]
public class ServiceController : Controller
{
    private readonly IPermissionService _perms;

    public ServiceController(IPermissionService perms) => _perms = perms;

    [HttpGet]
    public async Task<IActionResult> Show(string id)
    {
        var svc = ServiceCatalog.ById(id);
        if (svc == null) return NotFound();

        // Re-route to a dedicated controller for the system services that have real UI.
        var redirect = id switch
        {
            "manage-companies"          => Url.Action("Index", "Companies"),
            "manage-branches"           => Url.Action("Index", "Branches"),
            "manage-jobs"               => Url.Action("Index", "JobTitles"),
            "manage-departments"        => Url.Action("Index", "Departments"),
            "manage-sponsors"           => Url.Action("Index", "Sponsors"),
            "manage-tickets"            => Url.Action("Index", "Tickets"),
            "view-login-log"            => Url.Action("Index", "LoginLog"),
            "view-activity-log"         => Url.Action("Index", "ActivityLog"),
            "settings-hr-roles"         => Url.Action("Index", "HrRoles"),
            "settings-role-assignments" => Url.Action("Index", "RoleAssignments"),
            "employees"                 => Url.Action("Index", "Employees"),
            "dashboard"                 => Url.Action("Index", "HrDashboard"),
            "attendance"                => Url.Action("Index", "Attendance"),
            "leaves"                    => Url.Action("Index", "Leaves"),
            "payroll"                   => Url.Action("Index", "Payroll"),
            "contracts"                 => Url.Action("Index", "Contracts"),
            "advances"                  => Url.Action("Index", "Advances"),
            _                           => null,
        };
        if (redirect != null) return Redirect(redirect);

        var scope = ScopeCookieMiddleware.ReadFromCookie(HttpContext) ?? HrScope.Global();
        var allowed = await _perms.CanAsync(User, id, HrBusinessPermissions.View, scope);
        if (!allowed) return Forbid();

        ViewData["CurrentServiceId"] = id;
        return View("Placeholder", svc);
    }
}
