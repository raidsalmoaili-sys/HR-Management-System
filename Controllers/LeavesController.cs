using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Modules;
using Project_HR_1.Models.ViewModels.Modules;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

/// <summary>الإجازات — requests, approval workflow and annual-leave balances. See docs/features/hr-core-modules.md</summary>
public class LeavesController : HrModuleControllerBase
{
    protected override string ServiceId => "leaves";

    public LeavesController(AppDbContext db, IPermissionService perms, IScopeContext scope, IActivityLogger activity)
        : base(db, perms, scope, activity) { }

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null, string? type = null, string? q = null)
    {
        if (!await CanAsync(HrBusinessPermissions.View)) return Forbid();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var all = Scope.Filter(Db.LeaveRequests.AsNoTracking());
        var query = all.Include(l => l.Employee).ThenInclude(e => e!.Branch).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrEmpty(type)) query = query.Where(l => l.LeaveType == type);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(l => l.Employee!.NameAr.Contains(t) || l.Employee.NameEn.Contains(t) || l.Employee.EmployeeCode.Contains(t));
        }

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        return View(new LeavesListVM
        {
            Requests = await query.OrderBy(l => l.Status == RequestStatuses.Pending ? 0 : 1)
                                  .ThenByDescending(l => l.StartDate).Take(300).ToListAsync(),
            Employees = await EmployeePicksAsync(),
            Status = status, Type = type, Search = q,
            PendingCount = await all.CountAsync(l => l.Status == RequestStatuses.Pending),
            OnLeaveToday = await all.CountAsync(l => l.Status == RequestStatuses.Approved && l.StartDate <= today && l.EndDate >= today),
            UpcomingCount = await all.CountAsync(l => l.Status == RequestStatuses.Approved && l.StartDate > today),
            ApprovedThisMonthDays = await all.Where(l => l.Status == RequestStatuses.Approved && l.StartDate <= monthEnd && l.EndDate >= monthStart)
                                             .SumAsync(l => l.Days),
            Perms = await PermissionsAsync(),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeaveCreateVM m)
    {
        if (!await CanAsync(HrBusinessPermissions.Create)) return Forbid();
        if (!ModelState.IsValid || LeaveTypes.All.All(t => t.Code != m.LeaveType))
        {
            Flash("بيانات الطلب غير مكتملة", "The request is incomplete", true);
            return RedirectToAction(nameof(Index));
        }
        if (m.EndDate < m.StartDate)
        {
            Flash("تاريخ النهاية قبل تاريخ البداية", "End date is before start date", true);
            return RedirectToAction(nameof(Index));
        }

        var emp = await ScopedEmployeeAsync(m.EmployeeId);
        if (emp == null) return NotFound();

        var days = m.EndDate.DayNumber - m.StartDate.DayNumber + 1;
        if (m.LeaveType == LeaveTypes.Annual && emp.Work != null && days > emp.Work.AnnualLeaveBalance)
        {
            Flash($"رصيد الإجازة السنوية للموظف {emp.Work.AnnualLeaveBalance} يوم فقط، والطلب {days} يوم",
                  $"Annual balance is {emp.Work.AnnualLeaveBalance} days; request is {days} days", true);
            return RedirectToAction(nameof(Index));
        }

        var overlaps = await Db.LeaveRequests.AnyAsync(l => l.EmployeeId == emp.Id
            && (l.Status == RequestStatuses.Pending || l.Status == RequestStatuses.Approved)
            && l.StartDate <= m.EndDate && l.EndDate >= m.StartDate);
        if (overlaps)
        {
            Flash("يوجد طلب إجازة آخر يتداخل مع هذه الفترة", "Another leave request overlaps this period", true);
            return RedirectToAction(nameof(Index));
        }

        var req = new LeaveRequest
        {
            EmployeeId = emp.Id, CompanyId = emp.CompanyId, BranchId = emp.BranchId,
            LeaveType = m.LeaveType, StartDate = m.StartDate, EndDate = m.EndDate, Days = days,
            Reason = m.Reason?.Trim(), CreatedByUserId = CurrentUserId,
        };
        Db.LeaveRequests.Add(req);
        await Db.SaveChangesAsync();
        await Activity.LogAsync("LeaveRequest", req.Id.ToString(), "Create", employeeId: emp.Id, companyId: emp.CompanyId, branchId: emp.BranchId);
        Flash("تم تسجيل طلب الإجازة وهو بانتظار الاعتماد", "Leave request created and awaiting approval");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? note)
    {
        if (!await CanAsync(HrBusinessPermissions.Approve)) return Forbid();
        var req = await Scope.Filter(Db.LeaveRequests).FirstOrDefaultAsync(l => l.Id == id);
        if (req == null) return NotFound();
        if (req.Status != RequestStatuses.Pending) return RedirectToAction(nameof(Index));

        var emp = await Db.Employees.Include(e => e.Work).FirstAsync(e => e.Id == req.EmployeeId);
        if (emp.Work == null) { emp.Work = new EmployeeWork { EmployeeId = emp.Id }; }

        if (req.LeaveType == LeaveTypes.Annual)
        {
            if (req.Days > emp.Work.AnnualLeaveBalance)
            {
                Flash("لا يكفي رصيد الإجازة السنوية لاعتماد الطلب", "Not enough annual balance to approve", true);
                return RedirectToAction(nameof(Index));
            }
            emp.Work.AnnualLeaveBalance -= req.Days;
            emp.Work.LastLeaveDate = req.StartDate;
            req.BalanceDeducted = true;
        }
        else if (req.LeaveType == LeaveTypes.Sick)
        {
            emp.Work.SickLeaveUsedDays += req.Days;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (req.StartDate <= today && req.EndDate >= today && emp.Status == EmployeeStatuses.Active)
            emp.Status = EmployeeStatuses.Leave;

        req.Status = RequestStatuses.Approved;
        req.ReviewedByUserId = CurrentUserId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = note?.Trim();
        await Db.SaveChangesAsync();
        await Activity.LogAsync("LeaveRequest", id.ToString(), "Approve", employeeId: emp.Id, companyId: emp.CompanyId, branchId: emp.BranchId);
        Flash("تم اعتماد الإجازة", "Leave approved");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? note)
    {
        if (!await CanAsync(HrBusinessPermissions.Approve)) return Forbid();
        var req = await Scope.Filter(Db.LeaveRequests).FirstOrDefaultAsync(l => l.Id == id);
        if (req == null) return NotFound();
        if (req.Status != RequestStatuses.Pending) return RedirectToAction(nameof(Index));
        req.Status = RequestStatuses.Rejected;
        req.ReviewedByUserId = CurrentUserId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewNote = note?.Trim();
        await Db.SaveChangesAsync();
        await Activity.LogAsync("LeaveRequest", id.ToString(), "Reject", employeeId: req.EmployeeId, companyId: req.CompanyId, branchId: req.BranchId);
        Flash("تم رفض الطلب", "Request rejected");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!await CanAsync(HrBusinessPermissions.Edit)) return Forbid();
        var req = await Scope.Filter(Db.LeaveRequests).FirstOrDefaultAsync(l => l.Id == id);
        if (req == null) return NotFound();
        if (req.Status is not (RequestStatuses.Pending or RequestStatuses.Approved)) return RedirectToAction(nameof(Index));

        var emp = await Db.Employees.Include(e => e.Work).FirstAsync(e => e.Id == req.EmployeeId);
        if (req.BalanceDeducted && emp.Work != null)
        {
            emp.Work.AnnualLeaveBalance += req.Days;
            req.BalanceDeducted = false;
        }
        if (req.Status == RequestStatuses.Approved && req.LeaveType == LeaveTypes.Sick && emp.Work != null)
            emp.Work.SickLeaveUsedDays = Math.Max(0, emp.Work.SickLeaveUsedDays - req.Days);

        req.Status = RequestStatuses.Cancelled;
        await Db.SaveChangesAsync();

        // Back to work if no other approved leave covers today.
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (emp.Status == EmployeeStatuses.Leave &&
            !await Db.LeaveRequests.AnyAsync(l => l.EmployeeId == emp.Id && l.Status == RequestStatuses.Approved && l.StartDate <= today && l.EndDate >= today))
        {
            emp.Status = EmployeeStatuses.Active;
            await Db.SaveChangesAsync();
        }

        await Activity.LogAsync("LeaveRequest", id.ToString(), "Cancel", employeeId: emp.Id, companyId: emp.CompanyId, branchId: emp.BranchId);
        Flash("تم إلغاء الإجازة وإرجاع الرصيد", "Leave cancelled and balance restored");
        return RedirectToAction(nameof(Index));
    }
}
