using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.ViewModels.Entry;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Controllers;

[Authorize]
public class EntryController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _perms;

    public EntryController(AppDbContext db, IPermissionService perms)
    {
        _db = db;
        _perms = perms;
    }

    // ─── Dispatch after login ─────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Resume()
    {
        var kind = await _perms.ResolveAsync(User);
        if (kind == UserKind.None) return View("NoAccess");

        // Always start at Company select — user explicitly picks scope on each login.
        ScopeCookieMiddleware.Clear(HttpContext);
        return RedirectToAction("Welcome");
    }

    // ─── Step 1: Company ──────────────────────────────────────────────

    [HttpGet]
    public Task<IActionResult> Welcome() => Company();

    [HttpGet]
    public async Task<IActionResult> Company()
    {
        var companies = await _perms.VisibleCompaniesAsync(User);
        ViewData["Title"] = L("chooseCompanyH", "Choose Company");
        ViewData["Stepper"] = StepperVM.Build("company");
        return View("Company", new CompanyStepVM { Companies = companies });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChooseCompany(int companyId)
        => RedirectToAction("Branch", new { companyId });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult EnterGlobal()
    {
        ScopeCookieMiddleware.Write(HttpContext, HrScope.Global());
        return RedirectToAction("Launcher");
    }

    // ─── Step 2: Branch ───────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Branch(int companyId)
    {
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId);
        if (company == null) return RedirectToAction("Welcome");

        var branches = await _perms.VisibleBranchesAsync(User, companyId);
        ViewData["Title"] = L("chooseBranchH", "Choose Branch");
        ViewData["Stepper"] = StepperVM.Build("branch");
        return View("Branch", new BranchStepVM { Company = company, Branches = branches });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseBranch(int companyId, int branchId)
    {
        var branches = await _perms.VisibleBranchesAsync(User, companyId);
        if (!branches.Any(b => b.Id == branchId)) return Forbid();

        ScopeCookieMiddleware.Write(HttpContext, new HrScope
        {
            Level = ScopeLevel.Branch,
            CompanyId = companyId,
            BranchId = branchId,
        });
        return RedirectToAction("Launcher");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnterCompanyScope(int companyId)
    {
        var companies = await _perms.VisibleCompaniesAsync(User);
        if (!companies.Any(c => c.Id == companyId)) return Forbid();

        ScopeCookieMiddleware.Write(HttpContext, new HrScope
        {
            Level = ScopeLevel.Company,
            CompanyId = companyId,
        });
        return RedirectToAction("Launcher");
    }

    // ─── Step 3: Launcher ─────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Launcher(string? q = null, string? cat = null)
    {
        var scope = ScopeCookieMiddleware.ReadFromCookie(HttpContext) ?? HrScope.Global();
        var allowed = await _perms.AllowedServicesAsync(User, scope);

        var services = ServiceCatalog.Services
            .Where(s => cat == null || s.Category == cat)
            .Where(s => string.IsNullOrEmpty(q)
                        || s.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || s.NameAr.Contains(q)
                        || s.NameEn.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Resolve scope context entities for the banner.
        Company? scopedCompany = null;
        Branch?  scopedBranch  = null;
        if (scope.CompanyId.HasValue)
            scopedCompany = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == scope.CompanyId.Value);
        if (scope.BranchId.HasValue)
            scopedBranch  = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == scope.BranchId.Value);

        // ── Dashboard data ─────────────────────────────────────────────
        var empQuery = _db.Employees.AsNoTracking()
            .Include(e => e.Branch)
            .Where(e => e.Status == "active");

        if (scope.Level == ScopeLevel.Branch && scope.BranchId.HasValue)
            empQuery = empQuery.Where(e => e.BranchId == scope.BranchId.Value);
        else if (scope.CompanyId.HasValue)
            empQuery = empQuery.Where(e => e.CompanyId == scope.CompanyId.Value);

        var employees = await empQuery.ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Stats
        var saudiCount = employees.Count(e => e.NationalityCode == "SA" || string.IsNullOrEmpty(e.NationalityCode));
        var stats = new DashboardStats
        {
            TotalEmployees = employees.Count,
            SaudiCount     = saudiCount,
            NonSaudiCount  = employees.Count - saudiCount,
        };

        // Attendance + leaves (see docs/features/hr-core-modules.md)
        var attQ = _db.AttendanceRecords.AsNoTracking().Where(a => a.Date == today);
        var leaveQ = _db.LeaveRequests.AsNoTracking()
            .Where(l => l.Status == Project_HR_1.Models.Hr.Modules.RequestStatuses.Approved && l.StartDate <= today && l.EndDate >= today);
        if (scope.Level == ScopeLevel.Branch && scope.BranchId.HasValue)
        {
            attQ = attQ.Where(a => a.BranchId == scope.BranchId.Value);
            leaveQ = leaveQ.Where(l => l.BranchId == scope.BranchId.Value);
        }
        else if (scope.CompanyId.HasValue)
        {
            attQ = attQ.Where(a => a.CompanyId == scope.CompanyId.Value);
            leaveQ = leaveQ.Where(l => l.CompanyId == scope.CompanyId.Value);
        }
        stats.AttendanceToday = await attQ.CountAsync(a => a.Status == "present" || a.Status == "late" || a.Status == "remote");
        stats.ActiveLeaves = await leaveQ.CountAsync();

        // Expiry alerts (within 90 days)
        var alerts = new List<DocExpiryAlert>();
        foreach (var e in employees)
        {
            void AddAlert(DateOnly? expiry, string docType)
            {
                if (expiry == null) return;
                var days = expiry.Value.DayNumber - today.DayNumber;
                if (days > 90 || days < 0) return;
                alerts.Add(new DocExpiryAlert
                {
                    EmployeeId = e.Id,
                    NameAr     = e.NameAr,
                    NameEn     = e.NameEn,
                    DocType    = docType,
                    ExpiryDate = expiry.Value,
                    DaysLeft   = days,
                    Urgency    = days < 14 ? "critical" : days < 30 ? "warning" : "soon",
                });
            }
            AddAlert(e.IqamaExpiry,      "iqama");
            AddAlert(e.NationalIdExpiry, "national-id");
            AddAlert(e.PassportExpiry,   "passport");
            AddAlert(e.ContractEndDate,  "contract");
        }
        alerts = alerts.OrderBy(a => a.DaysLeft).ToList();
        stats.PendingAlerts = alerts.Count(a => a.Urgency == "critical");

        // Nationality breakdown (top 5 + other)
        var natGroups = employees
            .GroupBy(e => string.IsNullOrEmpty(e.NationalityCode) ? "SA" : e.NationalityCode)
            .Select(g => new NationalityCount
            {
                Label = g.Key == "SA" ? "سعودي" : (employees.FirstOrDefault(e => e.NationalityCode == g.Key)?.NationalityAr ?? g.Key),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToList();

        // Dept breakdown
        var deptGroups = employees
            .Where(e => !string.IsNullOrEmpty(e.DeptAr))
            .GroupBy(e => e.DeptAr!)
            .Select(g => new DeptCount { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToList();

        // Records that need a manual fix: no ID/Iqama, placeholder branch, nationality vs ID-type mismatch.
        var dataIssues = new List<DataIssueAlert>();
        foreach (var e in employees)
        {
            void Add(string kind, string note) => dataIssues.Add(new DataIssueAlert
            {
                EmployeeId = e.Id, EmployeeCode = e.EmployeeCode, NameAr = e.NameAr, NameEn = e.NameEn, Kind = kind, Note = note,
            });
            var hasNid = !string.IsNullOrWhiteSpace(e.NationalId);
            var hasIqama = !string.IsNullOrWhiteSpace(e.IqamaNumber);
            if (!hasNid && !hasIqama) Add("no-id", "لا يوجد رقم هوية/إقامة");
            if (e.Branch == null || e.Branch.NameAr == "فرع غير محدد") Add("no-branch", "غير مرتبط بفرع");
            if (e.NationalityCode == "SA" && !hasNid && hasIqama) Add("id-mismatch", $"الجنسية سعودي لكن الرقم {e.IqamaNumber} رقم إقامة");
            if (!string.IsNullOrEmpty(e.NationalityCode) && e.NationalityCode != "SA" && hasNid)
                Add("id-mismatch", $"الجنسية {e.NationalityAr} لكن الرقم {e.NationalId} رقم هوية وطنية");
        }

        ViewData["Title"] = L("launcherH", "HR Services Hub");
        ViewData["Stepper"] = StepperVM.Build("launcher");

        return View(new LauncherVM
        {
            Search = q,
            Category = cat,
            Services = services,
            AllowedServiceIds = allowed,
            Categories = ServiceCatalog.Categories.ToList(),
            Scope = scope,
            ScopeCompany = scopedCompany,
            ScopeBranch  = scopedBranch,
            Stats                = stats,
            ExpiryAlerts         = alerts,
            NationalityBreakdown = natGroups,
            DeptBreakdown        = deptGroups,
            DataIssues           = dataIssues,
        });
    }

    [AcceptVerbs("GET", "POST")]
    public IActionResult ChangeContext()
    {
        ScopeCookieMiddleware.Clear(HttpContext);
        return RedirectToAction("Welcome");
    }

    // ─── Scope strip chips (in-app shell) ────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchScope(string level, int? companyId = null, int? branchId = null)
    {
        var lvl = Enum.TryParse<ScopeLevel>(level, true, out var v) ? v : ScopeLevel.Global;

        if (lvl == ScopeLevel.Company && companyId.HasValue)
        {
            var companies = await _perms.VisibleCompaniesAsync(User);
            if (!companies.Any(c => c.Id == companyId.Value)) return Forbid();
        }
        else if (lvl == ScopeLevel.Branch && companyId.HasValue && branchId.HasValue)
        {
            var branches = await _perms.VisibleBranchesAsync(User, companyId.Value);
            if (!branches.Any(b => b.Id == branchId.Value)) return Forbid();
        }
        else if (lvl != ScopeLevel.Global)
        {
            return Forbid();
        }

        ScopeCookieMiddleware.Write(HttpContext, new HrScope { Level = lvl, CompanyId = companyId, BranchId = branchId });
        return RedirectToAction("Launcher");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Reset() => ChangeContext();

    private static string L(string key, string fallback) => fallback;
}
