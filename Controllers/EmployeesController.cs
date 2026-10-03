using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.ViewModels.Management;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Documents;
using Project_HR_1.Services.I18n;
using Project_HR_1.Services.Import;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Reports;
using Project_HR_1.Services.Scope;
using System.Drawing;

namespace Project_HR_1.Controllers;

[Authorize]
public class EmployeesController : Controller
{
    private const string ListSvc = "employees";
    private const string CreateSvc = "add-employee";
    private const string TransferSvc = "transfers";
    private const long MaxDocBytes = 5 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPermissionService _perms;
    private readonly IScopeContext _scope;
    private readonly IActivityLogger _activity;
    private readonly IContractPdfBuilder _contractPdf;
    private readonly IRosterPdfBuilder _rosterPdf;
    private readonly IHrLocalizer _l;

    public EmployeesController(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        IPermissionService perms,
        IScopeContext scope,
        IActivityLogger activity,
        IContractPdfBuilder contractPdf,
        IRosterPdfBuilder rosterPdf,
        IHrLocalizer l)
    {
        _db = db;
        _users = users;
        _perms = perms;
        _scope = scope;
        _activity = activity;
        _contractPdf = contractPdf;
        _rosterPdf = rosterPdf;
        _l = l;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] EmployeeListFilter? f)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();
        return View(await BuildListVmAsync(new AddEmployeeVM(), f));
    }

    // Shared by Index GET and by Create POST when validation fails — the Add Employee modal lives
    // on the Index page, so on error we re-render Index with the posted form (instead of redirecting
    // and losing everything the user typed across the 6 tabs).
    private async Task<EmployeesListVM> BuildListVmAsync(AddEmployeeVM addVm, EmployeeListFilter? f = null)
    {
        // Search + filters on the Employees list (docs/features/employee-list-filters.md).
        // Everything runs in SQL after the scope filter, so it can only narrow what the user may see.
        f ??= new EmployeeListFilter();
        var baseQuery = _scope.Filter(_db.Employees.AsNoTracking());
        var total = await baseQuery.CountAsync();

        var filtered = baseQuery;
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var q = f.Q.Trim();
            filtered = filtered.Where(e =>
                e.NameAr.Contains(q) || e.NameEn.Contains(q) || e.EmployeeCode.Contains(q) ||
                (e.Email != null && e.Email.Contains(q)) || (e.Phone != null && e.Phone.Contains(q)) ||
                (e.NationalId != null && e.NationalId.Contains(q)) || (e.IqamaNumber != null && e.IqamaNumber.Contains(q)) ||
                (e.JobTitleAr != null && e.JobTitleAr.Contains(q)));
        }
        if (f.CompanyId.HasValue) filtered = filtered.Where(e => e.CompanyId == f.CompanyId);
        if (f.BranchId.HasValue) filtered = filtered.Where(e => e.BranchId == f.BranchId);
        if (f.Nationality == "SA") filtered = filtered.Where(e => e.NationalityCode == "SA");
        else if (f.Nationality == "NON_SA") filtered = filtered.Where(e => e.NationalityCode != "SA");

        // Counts per status for the chips = every filter except the status itself.
        var statusCounts = await filtered.GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        if (EmployeeStatuses.IsValid(f.Status)) filtered = filtered.Where(e => e.Status == f.Status);

        var employees = await filtered
            .Include(e => e.Company)
            .Include(e => e.Branch)
            .Include(e => e.JobTitle)
            .OrderBy(e => e.NameAr)
            .ToListAsync();

        // When searching, rank by WHERE the term sits in the name (partial words are fine: "خال" finds خالد):
        //   exact employee code → 1st name starts with it → 2nd (father) → 3rd (grandfather) → 4th… →
        //   term inside a word (not at its start) → other fields (ID, phone, email). Alphabetical within a rank.
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var q = f.Q.Trim();
            static int NameRank(string? name, string q)
            {
                if (string.IsNullOrWhiteSpace(name)) return int.MaxValue;
                var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < words.Length; i++)
                    if (string.Join(' ', words[i..]).StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 10 + i;
                for (int i = 0; i < words.Length; i++)
                    if (words[i].Contains(q, StringComparison.OrdinalIgnoreCase)) return 50 + i;
                return int.MaxValue;
            }
            int Rank(Employee e) =>
                string.Equals(e.EmployeeCode, q, StringComparison.OrdinalIgnoreCase) ? 0
                : Math.Min(Math.Min(NameRank(e.NameAr, q), NameRank(e.NameEn, q)), 100);
            employees = employees.OrderBy(Rank).ThenBy(e => e.NameAr).ToList();
        }

        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);
        var filterCompanies = await companiesQuery.ToListAsync();
        var filterBranches = await _scope.Filter(_db.Branches.AsNoTracking()).OrderBy(b => b.NameAr).ToListAsync();

        var canCreate = await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current);
        if (canCreate)
        {
            await PopulateLookupsAsync(addVm);
        }
        ViewData["Title"] = "Employees";
        ViewData["CurrentServiceId"] = ListSvc;
        return new EmployeesListVM
        {
            Employees = employees,
            Filter = f,
            TotalCount = total,
            StatusCounts = statusCounts,
            FilterCompanies = filterCompanies,
            FilterBranches = filterBranches,
            CanCreate = canCreate,
            CanDelete = await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.Delete, _scope.Current),
            CanEditStatus = await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Edit, _scope.Current),
            AddEmployee = addVm,
        };
    }

    // Re-renders Index with the Add Employee modal re-opened and the errors shown inside it.
    private async Task<IActionResult> AddFailedAsync(AddEmployeeVM m, string? error = null)
    {
        if (error != null) ModelState.AddModelError(string.Empty, error);
        ViewData["reopenAddModal"] = true;
        return View(nameof(Index), await BuildListVmAsync(m));
    }

    private async Task PopulateLookupsAsync(AddEmployeeVM addVm)
    {
        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);
        addVm.Companies   = await companiesQuery.ToListAsync();
        addVm.Branches    = await _scope.Filter(_db.Branches.AsNoTracking()).OrderBy(b => b.NameAr).ToListAsync();
        addVm.JobTitles   = await _scope.Filter(_db.JobTitles.AsNoTracking()).OrderBy(j => j.NameAr).ToListAsync();
        addVm.Departments = await _scope.Filter(_db.Departments.AsNoTracking()).OrderBy(d => d.NameAr).ToListAsync();
        addVm.Sponsors    = await _scope.Filter(_db.Sponsors.AsNoTracking()).OrderBy(s => s.NameAr).ToListAsync();
        addVm.Supervisors = await _scope.Filter(_db.Employees.AsNoTracking()).OrderBy(e => e.NameAr).ToListAsync();
    }

    [HttpGet("Employees/Detail/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();
        var e = await _scope.Filter(_db.Employees.AsNoTracking())
            .Include(x => x.Company)
            .Include(x => x.Branch)
            .Include(x => x.JobTitle)
            .Include(x => x.Department)
            .Include(x => x.Sponsor)
            .Include(x => x.Personal)
            .Include(x => x.Financial)
            .Include(x => x.Work)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        var assignments = await _db.EmployeeRoleAssignments.AsNoTracking()
            .Include(a => a.HrRole)
            .Include(a => a.Company)
            .Include(a => a.Branch)
            .Where(a => a.EmployeeId == id)
            .ToListAsync();
        ViewData["Title"] = e.NameAr;

        var canTransfer = await _perms.CanAsync(User, TransferSvc, HrBusinessPermissions.Edit, _scope.Current);
        var transferVm = new TransferEmployeeVM();
        if (canTransfer)
        {
            await PopulateTransferLookupsAsync(transferVm);
        }

        var canEdit = await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Edit, _scope.Current);
        var loginEmail = e.UserId == null ? null : (await _users.FindByIdAsync(e.UserId))?.Email;
        var canAssignRoles = await _perms.CanAsync(User, "settings-role-assignments", HrBusinessPermissions.Create);
        return View(new EmployeeDetailVM { Employee = e, Assignments = assignments, CanTransfer = canTransfer, Transfer = transferVm, CanEdit = canEdit,
            LoginEmail = loginEmail, CanAssignRoles = canAssignRoles });
    }

    private async Task PopulateTransferLookupsAsync(TransferEmployeeVM vm)
    {
        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);
        vm.Companies = await companiesQuery.ToListAsync();
        vm.Branches  = await _scope.Filter(_db.Branches.AsNoTracking()).OrderBy(b => b.NameAr).ToListAsync();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(int id, TransferEmployeeVM m)
    {
        if (!await _perms.CanAsync(User, TransferSvc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();

        var employee = await _scope.Filter(_db.Employees)
            .Include(e => e.Company)
            .Include(e => e.Branch)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (employee == null) return NotFound();

        var branch = await _db.Branches.AsNoTracking().Include(b => b.Company).FirstOrDefaultAsync(b => b.Id == m.BranchId);
        if (branch == null)
        {
            TempData["err"] = "لم يتم العثور على الفرع المحدد.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        if (_scope.Current.BranchId.HasValue && branch.Id != _scope.Current.BranchId.Value)
        {
            TempData["err"] = "لا يمكنك نقل الموظف إلى فرع خارج نطاق صلاحيتك الحالي.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        if (_scope.Current.CompanyId.HasValue && branch.CompanyId != _scope.Current.CompanyId.Value)
        {
            TempData["err"] = "لا يمكنك نقل الموظف إلى شركة خارج نطاق صلاحيتك الحالي.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        if (branch.Id == employee.BranchId)
        {
            TempData["err"] = "الموظف موجود بالفعل في هذا الفرع.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var oldCompanyName = employee.Company?.NameAr;
        var oldBranchName = employee.Branch?.NameAr;

        employee.CompanyId = branch.CompanyId;
        employee.BranchId = branch.Id;
        await _db.SaveChangesAsync();

        var details = $"من: {oldCompanyName} / {oldBranchName} — إلى: {branch.Company?.NameAr} / {branch.NameAr}"
            + (string.IsNullOrWhiteSpace(m.Reason) ? "" : $" — السبب: {m.Reason}");
        await _activity.LogAsync("Employee", employee.Id.ToString(), "Transfer",
            userId: _users.GetUserId(User), employeeId: employee.Id, details: details,
            companyId: branch.CompanyId, branchId: branch.Id);

        TempData["ok"] = "تم نقل الموظف بنجاح.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // Change an employee's work status (على رأس العمل / مجاز / مستقيل / هارب).
    // Used from the status card on Employees/Detail and the status badge on Employees/Index.
    // ── Login account for an existing employee (docs/features/employee-login-account.md) ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLogin(int id, string? email, string? password)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        var e = await _scope.Filter(_db.Employees).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        IActionResult Fail(string msg) { TempData["err"] = msg; TempData["reopenLogin"] = true; return RedirectToAction(nameof(Detail), new { id }); }

        if (e.UserId != null) return Fail("هذا الموظف لديه حساب دخول مسبقاً.");
        email = email?.Trim();
        if (string.IsNullOrEmpty(email)) return Fail("أدخل البريد الإلكتروني.");
        if (string.IsNullOrEmpty(password)) return Fail("أدخل كلمة المرور.");
        if (await _users.FindByEmailAsync(email) != null || await _db.Employees.AnyAsync(x => x.Email == email && x.Id != id))
            return Fail($"البريد الإلكتروني '{email}' مستخدم مسبقاً.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            await tx.RollbackAsync();
            return Fail(string.Join(" · ", result.Errors.Select(er => TranslateIdentityError(er, email))));
        }
        e.UserId = user.Id;
        e.Email = email;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _activity.LogAsync("Employee", e.Id.ToString(), "CreateLogin", userId: _users.GetUserId(User), employeeId: e.Id,
            details: email, companyId: e.CompanyId, branchId: e.BranchId);
        TempData["ok"] = _l.L($"تم إنشاء حساب دخول لـ {e.NameAr} ({email}). أعطه دوراً من «الصلاحيات».",
                              $"Login created for {e.NameEn} ({email}). Assign roles to give permissions.");
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetLoginPassword(int id, string? password)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        var e = await _scope.Filter(_db.Employees).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        IActionResult Fail(string msg) { TempData["err"] = msg; TempData["reopenPassword"] = true; return RedirectToAction(nameof(Detail), new { id }); }

        var user = e.UserId == null ? null : await _users.FindByIdAsync(e.UserId);
        if (user == null) return Fail("هذا الموظف ليس لديه حساب دخول.");
        if (string.IsNullOrEmpty(password)) return Fail("أدخل كلمة المرور الجديدة.");
        // Only a system admin may reset another admin's password.
        if ((await _users.IsInRoleAsync(user, IdentityRoles.SuperAdmin) || await _users.IsInRoleAsync(user, IdentityRoles.Admin))
            && !(User.IsInRole(IdentityRoles.SuperAdmin) || User.IsInRole(IdentityRoles.Admin)))
            return Forbid();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            return Fail(string.Join(" · ", result.Errors.Select(er => TranslateIdentityError(er, user.Email))));

        await _activity.LogAsync("Employee", e.Id.ToString(), "ResetPassword", userId: _users.GetUserId(User), employeeId: e.Id,
            details: user.Email, companyId: e.CompanyId, branchId: e.BranchId);
        TempData["ok"] = _l.L($"تم تغيير كلمة مرور {e.NameAr}.", $"Password changed for {e.NameEn}.");
        return RedirectToAction(nameof(Detail), new { id });
    }

    // See docs/features/employee-status.md
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string status, DateOnly? date, string? note, string? returnUrl)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        var e = await _scope.Filter(_db.Employees).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();

        IActionResult Back() => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Detail), new { id });

        if (!EmployeeStatuses.IsValid(status))
        {
            TempData["err"] = _l.L("حالة غير معروفة.", "Unknown status.");
            return Back();
        }

        var old = EmployeeStatuses.Get(e.Status);
        var neu = EmployeeStatuses.Get(status);
        // Resigned/absconded: the date is the employee's last working day. It can be corrected later by
        // submitting the same status again with a different date (the "تعديل آخر يوم" button).
        var sameStatus = e.Status == status;
        e.Status = status;
        if (EmployeeStatuses.EndsWork(status))
            e.LastWorkDate = date ?? (sameStatus ? e.LastWorkDate : null) ?? DateOnly.FromDateTime(DateTime.Today);
        else if (status == EmployeeStatuses.Active)
            e.LastWorkDate = null;   // back to work — no "last day" any more
        await _db.SaveChangesAsync();

        var details = (sameStatus ? $"{neu.NameAr} (تعديل آخر يوم)" : $"{old.NameAr} ← {neu.NameAr}")
            + (date.HasValue ? $" · {date:yyyy-MM-dd}" : "")
            + (string.IsNullOrWhiteSpace(note) ? "" : $" · {note.Trim()}");
        await _activity.LogAsync("Employee", e.Id.ToString(), "ChangeStatus", userId: _users.GetUserId(User), employeeId: e.Id,
            details: details.Length > 500 ? details[..500] : details, companyId: e.CompanyId, branchId: e.BranchId);

        TempData["ok"] = sameStatus && EmployeeStatuses.EndsWork(status)
            ? _l.L($"تم تعديل تاريخ آخر يوم لـ {e.NameAr} إلى {e.LastWorkDate:yyyy-MM-dd}.", $"Last working day updated to {e.LastWorkDate:yyyy-MM-dd}.")
            : _l.L($"تم تغيير حالة {e.NameAr} إلى «{neu.NameAr}».", $"{e.NameEn} is now {neu.NameEn}.");
        return Back();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBasics(int id, EditEmployeeBasicsVM m)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        var e = await _scope.Filter(_db.Employees).Include(x => x.Financial).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();

        static string? Digits(string? v) { var d = new string((v ?? "").Where(char.IsDigit).ToArray()); return d == "" ? null : d; }
        var errors = new List<string>();
        var nid = Digits(m.NationalId);
        var iqama = Digits(m.IqamaNumber);
        if (nid != null && (nid.Length != 10 || nid[0] != '1')) errors.Add("رقم الهوية الوطنية يجب أن يكون 10 أرقام ويبدأ بـ 1.");
        if (iqama != null && (iqama.Length != 10 || iqama[0] != '2')) errors.Add("رقم الإقامة يجب أن يكون 10 أرقام ويبدأ بـ 2.");
        var iban = string.IsNullOrWhiteSpace(m.Iban) ? null : new string(m.Iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (iban != null && !System.Text.RegularExpressions.Regex.IsMatch(iban, @"^SA\d{22}$"))
            errors.Add("الآيبان يجب أن يكون SA متبوعاً بـ 22 رقماً (24 خانة).");
        var email = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim().ToLowerInvariant();
        if (email != null && await _db.Employees.AnyAsync(x => x.Id != id && x.Email == email))
            errors.Add($"البريد {email} مستخدم لموظف آخر.");
        foreach (var dupId in new[] { nid, iqama }.Where(v => v != null))
        {
            var other = await _db.Employees.AsNoTracking().Where(x => x.Id != id && (x.NationalId == dupId || x.IqamaNumber == dupId))
                .Select(x => x.EmployeeCode).FirstOrDefaultAsync();
            if (other != null) errors.Add($"الرقم {dupId} مسجل مسبقاً للموظف رقم {other} — لا يمكن تكرار الهوية.");
        }
        if (errors.Count > 0)
        {
            TempData["err"] = string.Join(" · ", errors);
            TempData["reopenEdit"] = true;
            return RedirectToAction(nameof(Detail), new { id });
        }

        var nat = Lookups.Nationalities.FirstOrDefault(n => n.Code == m.NationalityCode);
        if (nat != null) { e.NationalityCode = nat.Code; e.NationalityAr = nat.NameAr; e.NationalityEn = nat.NameEn; }
        e.NationalId = nid; e.NationalIdExpiry = nid == null ? null : m.NationalIdExpiry;
        e.IqamaNumber = iqama; e.IqamaExpiry = iqama == null ? null : m.IqamaExpiry;
        e.PassportNumber = string.IsNullOrWhiteSpace(m.PassportNumber) ? null : m.PassportNumber.Trim();
        e.PassportExpiry = e.PassportNumber == null ? null : m.PassportExpiry;
        e.Phone = string.IsNullOrWhiteSpace(m.Phone) ? null : m.Phone.Trim();
        e.Email = email;
        e.JobTitleAr = string.IsNullOrWhiteSpace(m.JobTitleAr) ? null : m.JobTitleAr.Trim();
        e.JobTitleEn ??= e.JobTitleAr;
        if (m.BasicSalary.HasValue) e.BasicSalary = m.BasicSalary.Value;
        e.Financial ??= new EmployeeFinancial();
        e.Financial.BankName = string.IsNullOrWhiteSpace(m.BankName) ? null : m.BankName;
        e.Financial.Iban = iban;
        await _db.SaveChangesAsync();

        await _activity.LogAsync("Employee", e.Id.ToString(), "UpdateBasics", userId: _users.GetUserId(User), employeeId: e.Id,
            companyId: e.CompanyId, branchId: e.BranchId);

        var warn = (e.NationalityCode == "SA" && nid == null && iqama != null) || (e.NationalityCode != "SA" && nid != null)
            ? " — تنبيه: الجنسية ما زالت لا تطابق نوع الرقم." : "";
        TempData["ok"] = "تم حفظ البيانات." + warn;
        return RedirectToAction(nameof(Detail), new { id });
    }

    // GET /Employees/Create — bounce to Index, the modal lives there.
    [HttpGet]
    public IActionResult Create() => RedirectToAction(nameof(Index));

    // AJAX: check if email is already registered
    [HttpGet]
    public async Task<IActionResult> CheckEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Json(new { taken = false });
        var e = email.Trim();
        var taken = await _users.FindByEmailAsync(e) != null || await _db.Employees.AnyAsync(x => x.Email == e);
        return Json(new { taken });
    }

    // AJAX: check if employee code is already used
    [HttpGet]
    public async Task<IActionResult> CheckCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { taken = false });
        var taken = await _db.Employees.AnyAsync(e => e.EmployeeCode == code.Trim());
        return Json(new { taken });
    }

    // AJAX: generate the next available employee code given an optional prefix
    [HttpGet]
    public async Task<IActionResult> NextCode(string? prefix)
    {
        var p = (prefix ?? "").Trim();

        // Pull all codes that start with the prefix
        var codes = await _db.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeCode.StartsWith(p))
            .Select(e => e.EmployeeCode)
            .ToListAsync();

        // Find the highest numeric suffix
        int maxNum = 0;
        foreach (var code in codes)
        {
            var suffix = code.Length > p.Length ? code[p.Length..] : "";
            if (int.TryParse(suffix, out int n) && n > maxNum)
                maxNum = n;
        }

        // Return next number zero-padded to 3 digits (001, 002 …)
        return Json(new { code = $"{p}{maxNum + 1:D3}" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AddEmployeeVM m)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        if (_scope.Current.BranchId.HasValue && m.BranchId != _scope.Current.BranchId.Value)
        {
            TempData["err"] = "لا يمكنك إضافة موظف خارج نطاق صلاحيتك الحالي.";
            return RedirectToAction(nameof(Index));
        }

        ValidateDocumentUploads(m);

        // Login account is optional: both blank => employee record only (e.g. workers with no email).
        var email = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim();
        var password = string.IsNullOrEmpty(m.InitialPassword) ? null : m.InitialPassword;
        if (email != null && password == null)
            ModelState.AddModelError(nameof(m.InitialPassword), "أدخلت بريداً إلكترونياً — يجب إدخال كلمة مرور أولية لإنشاء حساب الدخول (أو امسح البريد).");
        if (email == null && password != null)
            ModelState.AddModelError(nameof(m.Email), "أدخلت كلمة مرور — يجب إدخال البريد الإلكتروني لإنشاء حساب الدخول (أو امسح كلمة المرور).");
        m.EmployeeCode = (m.EmployeeCode ?? "").Trim();

        if (!ModelState.IsValid) return await AddFailedAsync(m);

        if (email != null && (await _users.FindByEmailAsync(email) != null
                              || await _db.Employees.AnyAsync(e => e.Email == email)))
            return await AddFailedAsync(m, $"البريد الإلكتروني '{email}' مستخدم مسبقاً. يرجى استخدام بريد إلكتروني مختلف.");

        // Uniqueness is global (DB unique index), not per-scope.
        if (await _db.Employees.AnyAsync(e => e.EmployeeCode == m.EmployeeCode))
            return await AddFailedAsync(m, $"الرقم الوظيفي '{m.EmployeeCode}' مستخدم مسبقاً. يرجى اختيار رقم وظيفي مختلف.");

        // Derive company from the branch — UI only asks for branch.
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == m.BranchId);
        if (branch == null)
            return await AddFailedAsync(m, "لم يتم العثور على الفرع المحدد.");
        var derivedCompanyId = branch.CompanyId;
        if (_scope.Current.CompanyId.HasValue && derivedCompanyId != _scope.Current.CompanyId.Value)
            return await AddFailedAsync(m, "لا يمكنك إضافة موظف خارج نطاق صلاحيتك الحالي.");

        // Lookups are optional, but when chosen they must belong to the branch's company.
        async Task<bool> BelongsAsync<T>(IQueryable<T> set, int? id) where T : class, IScoped
        {
            if (!id.HasValue) return true;
            var item = await set.AsNoTracking().FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id.Value);
            return item != null && ((IScoped)item).CompanyId == derivedCompanyId;
        }
        if (!await BelongsAsync(_db.JobTitles, m.JobTitleId) || !await BelongsAsync(_db.JobTitles, m.ActualJobTitleId))
            return await AddFailedAsync(m, "المسمى الوظيفي يجب أن ينتمي لنفس شركة الفرع المحدد.");
        if (!await BelongsAsync(_db.Departments, m.DepartmentId))
            return await AddFailedAsync(m, "الإدارة يجب أن تنتمي لنفس شركة الفرع المحدد.");
        if (!await BelongsAsync(_db.Sponsors, m.SponsorId))
            return await AddFailedAsync(m, "الكفيل يجب أن ينتمي لنفس شركة الفرع المحدد.");

        // Identity uses the same AppDbContext, so one transaction covers both the login account
        // and the employee row — no orphan AspNetUsers row if saving the employee fails.
        await using var tx = await _db.Database.BeginTransactionAsync();

        ApplicationUser? user = null;
        if (email != null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await _users.CreateAsync(user, password!);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync();
                return await AddFailedAsync(m, string.Join(" · ", result.Errors.Select(e => TranslateIdentityError(e, email))));
            }
        }

        var nationality = Lookups.Nationalities.FirstOrDefault(n => n.Code == m.NationalityCode);
        var isSaudi = m.NationalityCode == "SA";
        var nameAr = string.Join(" ", new[] { m.FirstNameAr, m.FatherNameAr, m.GrandfatherNameAr, m.FamilyNameAr }
            .Where(p => !string.IsNullOrWhiteSpace(p)));
        var nameEnParts = new[] { m.FirstNameEn, m.FatherNameEn, m.GrandfatherNameEn, m.FamilyNameEn }
            .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var nameEn = nameEnParts.Count > 0 ? string.Join(" ", nameEnParts) : nameAr;

        var employee = new Employee
        {
            EmployeeCode = m.EmployeeCode,
            UserId = user?.Id,
            NameAr = nameAr, NameEn = nameEn, Initials = ComputeInitials(nameAr),
            FirstNameAr = m.FirstNameAr, FatherNameAr = m.FatherNameAr, GrandfatherNameAr = m.GrandfatherNameAr, FamilyNameAr = m.FamilyNameAr,
            FirstNameEn = m.FirstNameEn, FatherNameEn = m.FatherNameEn, GrandfatherNameEn = m.GrandfatherNameEn, FamilyNameEn = m.FamilyNameEn,
            CompanyId = derivedCompanyId, BranchId = m.BranchId,
            JobTitleId = m.JobTitleId,
            DepartmentId = m.DepartmentId,
            SponsorId = m.SponsorId,
            CostCenter = m.CostCenter,
            NationalityCode = m.NationalityCode,
            NationalityAr = nationality?.NameAr,
            NationalityEn = nationality?.NameEn,
            NationalId = m.NationalId, NationalIdExpiry = m.NationalIdExpiry,
            IqamaNumber = m.IqamaNumber, IqamaExpiry = m.IqamaExpiry,
            PassportNumber = m.PassportNumber, PassportExpiry = m.PassportExpiry,
            HireDate = m.HireDate, LastWorkDate = m.LastWorkDate,
            BasicSalary = m.BasicSalary, Phone = string.IsNullOrWhiteSpace(m.Phone) ? null : m.Phone.Trim(),
            ContractStartDate = m.ContractStartDate, ContractEndDate = m.ContractEndDate,
            Email = email,
            Status = "active",
            AvatarColor = "#1a2744",
            CreatedByUserId = _users.GetUserId(User),
            CreatedAt = DateTime.UtcNow,
        };

        employee.Personal = new EmployeePersonal
        {
            MaritalStatus = m.MaritalStatus,
            ChildrenCount = m.ChildrenCount,
            BloodType = m.BloodType,
            City = m.City, District = m.District, Street = m.Street,
            EmergencyContactName = m.EmergencyContactName, EmergencyContactPhone = m.EmergencyContactPhone,
            // Server-enforced: education fields are only kept when IsEducated = true.
            IsEducated = m.IsEducated,
            EducationLevel       = m.IsEducated ? m.EducationLevel : null,
            EducationMajor       = m.IsEducated ? m.EducationMajor : null,
            EducationInstitution = m.IsEducated ? m.EducationInstitution : null,
            EducationCountry     = m.IsEducated ? m.EducationCountry : null,
            GraduationYear       = m.IsEducated ? m.GraduationYear : null,
            Gpa                  = m.IsEducated ? m.Gpa : null,
            CertificateNumber    = m.IsEducated ? m.CertificateNumber : null,
        };

        employee.Financial = new EmployeeFinancial
        {
            BankName = m.BankName, Iban = m.Iban, BeneficiaryName = m.BeneficiaryName,
            HousingAllowance = m.HousingAllowance, TransportAllowance = m.TransportAllowance,
            OtherAllowance = m.OtherAllowance, OtherAllowanceDescription = m.OtherAllowanceDescription,
            // Server-enforced: only non-Saudi employees may have an end-of-service advance, regardless of what was posted.
            EndOfServiceAdvance = isSaudi ? null : m.EndOfServiceAdvance,
            SalaryType = m.SalaryType,
            DailyRate = m.SalaryType == SalaryType.Daily ? m.DailyRate : null,
            ContractTotalValue = m.SalaryType == SalaryType.Contract ? m.ContractTotalValue : null,
            ContractDurationMonths = m.SalaryType == SalaryType.Contract ? m.FinancialContractDurationMonths : null,
        };

        employee.Work = new EmployeeWork
        {
            ActualJobTitleId = m.ActualJobTitleId,
            DirectSupervisorId = m.DirectSupervisorId,
            WorkLocation = m.WorkLocation,
            WorkSystem = m.WorkSystem,
            DailyWorkHours = m.DailyWorkHours,
            ContractType = m.ContractType,
            ContractDurationMonths = m.ContractDurationMonths,
            ProbationDays = m.ProbationDays,
            AnnualLeaveBalance = m.AnnualLeaveBalance,
            LastLeaveDate = m.LastLeaveDate,
            SickLeaveUsedDays = m.SickLeaveUsedDays,
        };

        await BuildDocumentsAsync(employee, m);

        _db.Employees.Add(employee);
        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return await AddFailedAsync(m, "تعذّر حفظ الموظف — غالباً الرقم الوظيفي أو البريد أُضيف للتو من مستخدم آخر. راجع البيانات وحاول مرة أخرى.");
        }

        await _activity.LogAsync("Employee", employee.Id.ToString(), "Create",
            userId: _users.GetUserId(User), employeeId: employee.Id,
            companyId: derivedCompanyId, branchId: m.BranchId);

        if (m.SaveAndAddAnother)
        {
            TempData["ok"] = $"تمت إضافة الموظف '{employee.NameAr}' بنجاح. يمكنك إضافة موظف آخر.";
            TempData["reopenAddModal"] = true;
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Detail), new { id = employee.Id });
    }

    private void ValidateDocumentUploads(AddEmployeeVM m)
    {
        var files = new (IFormFile? File, string Field)[]
        {
            (m.NationalIdFile, nameof(m.NationalIdFile)), (m.PassportFile, nameof(m.PassportFile)),
            (m.IqamaFile, nameof(m.IqamaFile)), (m.DrivingLicenseFile, nameof(m.DrivingLicenseFile)),
            (m.CvFile, nameof(m.CvFile)), (m.ContractFile, nameof(m.ContractFile)),
            (m.EducationCertificateFile, nameof(m.EducationCertificateFile)), (m.IbanCertificateFile, nameof(m.IbanCertificateFile)),
        };
        foreach (var (file, field) in files)
            ValidateOneUpload(file, field);
        foreach (var file in m.CustomDocumentFiles)
            ValidateOneUpload(file, nameof(m.CustomDocumentFiles));
    }

    private void ValidateOneUpload(IFormFile? file, string field)
    {
        if (file == null || file.Length == 0) return;
        if (file.ContentType != "application/pdf")
            ModelState.AddModelError(field, "صيغة الملف يجب أن تكون PDF.");
        if (file.Length > MaxDocBytes)
            ModelState.AddModelError(field, "حجم الملف يجب ألا يتجاوز 5 ميغابايت.");
    }

    private async Task BuildDocumentsAsync(Employee employee, AddEmployeeVM m)
    {
        var userId = _users.GetUserId(User);

        async Task AddIfPresent(IFormFile? file, EmployeeDocType type)
        {
            if (file == null || file.Length == 0) return;
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            employee.Documents.Add(new EmployeeDocument
            {
                DocType = type,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileData = ms.ToArray(),
                FileSizeBytes = file.Length,
                UploadedByUserId = userId,
            });
        }

        await AddIfPresent(m.NationalIdFile, EmployeeDocType.NationalId);
        await AddIfPresent(m.PassportFile, EmployeeDocType.Passport);
        await AddIfPresent(m.IqamaFile, EmployeeDocType.Iqama);
        await AddIfPresent(m.DrivingLicenseFile, EmployeeDocType.DrivingLicense);
        await AddIfPresent(m.CvFile, EmployeeDocType.Cv);
        await AddIfPresent(m.ContractFile, EmployeeDocType.Contract);
        await AddIfPresent(m.EducationCertificateFile, EmployeeDocType.EducationCertificate);
        await AddIfPresent(m.IbanCertificateFile, EmployeeDocType.IbanCertificate);

        for (int i = 0; i < m.CustomDocumentFiles.Count; i++)
        {
            var file = m.CustomDocumentFiles[i];
            if (file.Length == 0) continue;
            var label = i < m.CustomDocumentLabels.Count ? m.CustomDocumentLabels[i] : null;
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            employee.Documents.Add(new EmployeeDocument
            {
                DocType = EmployeeDocType.Other,
                CustomLabel = label,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileData = ms.ToArray(),
                FileSizeBytes = file.Length,
                UploadedByUserId = userId,
            });
        }
    }

    // AJAX: generates a contract PDF preview from the in-progress (unsaved) form data.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewContract(AddEmployeeVM m)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();

        var branch = m.BranchId > 0 ? await _db.Branches.AsNoTracking().Include(b => b.Company).FirstOrDefaultAsync(b => b.Id == m.BranchId) : null;
        var jobTitle = m.ActualJobTitleId.HasValue
            ? await _db.JobTitles.AsNoTracking().FirstOrDefaultAsync(j => j.Id == m.ActualJobTitleId.Value)
            : (m.JobTitleId.HasValue ? await _db.JobTitles.AsNoTracking().FirstOrDefaultAsync(j => j.Id == m.JobTitleId.Value) : null);
        var nationality = Lookups.Nationalities.FirstOrDefault(n => n.Code == m.NationalityCode);
        var nameAr = string.Join(" ", new[] { m.FirstNameAr, m.FatherNameAr, m.GrandfatherNameAr, m.FamilyNameAr }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

        var data = new ContractPreviewData
        {
            EmployeeName = string.IsNullOrWhiteSpace(nameAr) ? "—" : nameAr,
            EmployeeCode = m.EmployeeCode,
            NationalityName = nationality?.NameAr,
            CompanyName = branch?.Company != null ? branch.Company.NameAr : "—",
            BranchName = branch?.NameAr ?? "—",
            JobTitleName = jobTitle?.NameAr,
            BasicSalary = m.BasicSalary,
            ContractTypeLabel = m.ContractType == ContractType.Limited ? "محدد المدة" : "غير محدد المدة",
            ContractStartDate = m.ContractStartDate,
            ContractEndDate = m.ContractEndDate,
            ContractDurationMonths = m.ContractDurationMonths,
            ProbationDays = m.ProbationDays,
        };

        var pdf = _contractPdf.Build(data);
        return File(pdf, "application/pdf", "contract-preview.pdf");
    }

    // GET: stream a saved document inline for the preview modal.
    [HttpGet]
    public async Task<IActionResult> ViewDocument(int id)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();
        var doc = await _db.EmployeeDocuments.AsNoTracking().Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == id);
        if (doc?.Employee == null) return NotFound();
        if (!_scope.Filter(_db.Employees.AsNoTracking()).Any(e => e.Id == doc.EmployeeId)) return Forbid();
        return File(doc.FileData, doc.ContentType, doc.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Edit, _scope.Current)) return Forbid();
        var doc = await _db.EmployeeDocuments.Include(d => d.Employee).FirstOrDefaultAsync(d => d.Id == id);
        if (doc?.Employee == null) return NotFound();
        if (!_scope.Filter(_db.Employees).Any(e => e.Id == doc.EmployeeId)) return Forbid();
        var employeeId = doc.EmployeeId;
        _db.EmployeeDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        await _activity.LogAsync("EmployeeDocument", id.ToString(), "Delete", employeeId: employeeId);
        return RedirectToAction(nameof(Detail), new { id = employeeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBulk(int[] ids)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.Delete, _scope.Current)) return Forbid();
        if (ids.Length == 0) return RedirectToAction(nameof(Index));

        var employees = await _scope.Filter(_db.Employees).Where(e => ids.Contains(e.Id)).ToListAsync();
        var userIds = employees.Select(e => e.UserId).Where(uid => uid != null).ToList();

        _db.Employees.RemoveRange(employees);
        await _db.SaveChangesAsync();

        foreach (var uid in userIds)
        {
            var user = await _users.FindByIdAsync(uid!);
            if (user != null) await _users.DeleteAsync(user);
        }

        await _activity.LogAsync("Employee", string.Join(",", ids), "BulkDelete");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.Delete, _scope.Current)) return Forbid();

        var employee = await _scope.Filter(_db.Employees).FirstOrDefaultAsync(x => x.Id == id);
        if (employee == null) return NotFound();

        var userId = employee.UserId;
        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync();

        if (userId != null)
        {
            var user = await _users.FindByIdAsync(userId);
            if (user != null) await _users.DeleteAsync(user);
        }

        await _activity.LogAsync("Employee", id.ToString(), "Delete");
        return RedirectToAction(nameof(Index));
    }

    // ─── Bulk Import ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> BulkImport()
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        var vm = new BulkImportVM
        {
            Branches = await _scope.Filter(_db.Branches.AsNoTracking())
                .Include(b => b.Company)
                .OrderBy(b => b.NameAr)
                .ToListAsync()
        };
        ViewData["Title"] = "Bulk Import";
        ViewData["CurrentServiceId"] = CreateSvc;
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadTemplate(string[]? cols, string[]? required, string? reqSubmitted)
    {
        var allCols  = EmployeeImportColumns.All;
        var sections = EmployeeImportColumns.Sections;
        var lights   = EmployeeImportColumns.SectionLightColors;

        int SectionIndexOf(int originalIdx1Based)
        {
            for (int s = 0; s < sections.Count; s++)
                if (originalIdx1Based >= sections[s].FromCol && originalIdx1Based <= sections[s].ToCol) return s;
            return sections.Count - 1;
        }

        // The admin has full control over which columns to include, even required ones —
        // if a required column is left out, the upload step rejects the whole file with a
        // clear "missing required column" message rather than silently guessing.
        List<(EmployeeImportColumn Col, int SecIdx, (int R, int G, int B) Light)> BuildSelected(HashSet<string>? wanted)
        {
            var list = new List<(EmployeeImportColumn Col, int SecIdx, (int R, int G, int B) Light)>();
            for (int i = 0; i < allCols.Count; i++)
            {
                var c = allCols[i];
                if (wanted != null && !wanted.Contains(c.Header)) continue;
                int si = SectionIndexOf(i + 1);
                list.Add((c, si, lights[si]));
            }
            return list;
        }

        var wanted = cols != null && cols.Length > 0
            ? new HashSet<string>(cols, StringComparer.OrdinalIgnoreCase)
            : null;

        var selected = BuildSelected(wanted);
        // Nothing matched (e.g. the admin deselected every column) => fall back to the full template
        // instead of generating a broken, columnless file.
        if (selected.Count == 0) selected = BuildSelected(null);

        // Which columns count as "required" is fully admin-controlled too (same "full authority to
        // the user" decision as column inclusion — see docs/features/employee-bulk-import.md). Unlike
        // inclusion, an empty required set is a perfectly valid choice (a fully-optional file), so we
        // can't just treat "nothing came through" as "use defaults" — the hidden reqSubmitted marker
        // tells us whether the picker form was actually submitted (honor `required` as-is, even empty)
        // versus a bare/direct link (fall back to each column's built-in default).
        var requiredSet = string.IsNullOrEmpty(reqSubmitted)
            ? allCols.Where(c => c.Required).Select(c => c.Header).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(required ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        // Group consecutive selected columns that share the same original section
        // so the section header band still merges correctly over the filtered layout.
        var effSections = new List<(ImportSection Sec, int FromCol, int ToCol, (int R, int G, int B) Light)>();
        for (int i = 0; i < selected.Count; i++)
        {
            var (col, si, light) = selected[i];
            if (effSections.Count > 0 && effSections[^1].Sec == sections[si])
            {
                var last = effSections[^1];
                effSections[^1] = (last.Sec, last.FromCol, i + 1, last.Light);
            }
            else
            {
                effSections.Add((sections[si], i + 1, i + 1, light));
            }
        }

        int total = selected.Count;

        // Pre-fetch branches and job titles for the Reference sheet
        var branches = await _scope.Filter(_db.Branches.AsNoTracking())
            .Include(b => b.Company)
            .OrderBy(b => b.NameAr)
            .ToListAsync();
        var jobTitlesForRef = await _scope.Filter(_db.JobTitles.AsNoTracking())
            .Include(j => j.Company)
            .OrderBy(j => j.NameAr)
            .ToListAsync();

        using var pkg = new ExcelPackage();

        // ══════════════════════════════════════════════════════════
        //  SHEET 1 — بيانات الموظفين
        // ══════════════════════════════════════════════════════════
        var ws = pkg.Workbook.Worksheets.Add("بيانات الموظفين");
        ws.View.RightToLeft = true;

        // ── Row 1: Title ─────────────────────────────────────────
        ws.Cells[1, 1, 1, total].Merge = true;
        ws.Cells[1, 1].Value = $"قالب استيراد بيانات الموظفين — نظام الموارد البشرية     |     {DateTime.Now:yyyy/MM/dd}";
        using (var r = ws.Cells[1, 1, 1, total])
        {
            r.Style.Font.Bold      = true;
            r.Style.Font.Size      = 13;
            r.Style.Font.Color.SetColor(Color.White);
            r.Style.Fill.PatternType = ExcelFillStyle.Solid;
            r.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(15, 23, 42));
            r.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            r.Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
        }
        ws.Row(1).Height = 28;

        // ── Row 2: Section group headers ─────────────────────────
        foreach (var es in effSections)
        {
            var sec = es.Sec;
            ws.Cells[2, es.FromCol, 2, es.ToCol].Merge = true;
            ws.Cells[2, es.FromCol].Value = sec.NameAr;
            using (var r = ws.Cells[2, es.FromCol, 2, es.ToCol])
            {
                r.Style.Font.Bold      = true;
                r.Style.Font.Size      = 10;
                r.Style.Font.Color.SetColor(Color.White);
                r.Style.Fill.PatternType = ExcelFillStyle.Solid;
                r.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(sec.R, sec.G, sec.B));
                r.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                r.Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
                r.Style.Border.Bottom.Style = ExcelBorderStyle.Medium;
                r.Style.Border.Bottom.Color.SetColor(Color.White);
            }
        }
        ws.Row(2).Height = 20;

        // ── Rows 3 & 4: Arabic + English column headers ───────────
        for (int c = 1; c <= total; c++)
        {
            var col   = selected[c - 1].Col;
            var sec   = sections[selected[c - 1].SecIdx];
            var light = selected[c - 1].Light;
            var isRequired = requiredSet.Contains(col.Header);

            var reqBg  = Color.FromArgb(sec.R, sec.G, sec.B);
            var optBg  = Color.FromArgb(light.R, light.G, light.B);
            var darkTxt = Color.FromArgb(30, 30, 50);

            // Row 3: Arabic name
            ws.Cells[3, c].Value = col.NameAr;
            using (var cell = ws.Cells[3, c])
            {
                cell.Style.Font.Bold = isRequired;
                cell.Style.Font.Size = 9;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(isRequired ? reqBg : optBg);
                cell.Style.Font.Color.SetColor(isRequired ? Color.White : darkTxt);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
                cell.Style.WrapText = true;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(200, 205, 220));
            }

            // Row 4: English name (small reference)
            ws.Cells[4, c].Value = col.Header;
            using (var cell = ws.Cells[4, c])
            {
                cell.Style.Font.Italic = true;
                cell.Style.Font.Size   = 7;
                cell.Style.Font.Color.SetColor(Color.FromArgb(100, 110, 130));
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(242, 244, 248));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                cell.Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.FromArgb(210, 215, 225));
            }
        }
        ws.Row(3).Height = 32;
        ws.Row(4).Height = 14;

        // ── Row 5+: Empty data rows (alternating) ────────────────
        var white    = Color.White;
        var altBg    = Color.FromArgb(248, 250, 255);
        var cellBdr  = Color.FromArgb(220, 225, 235);
        for (int row = 5; row <= 200; row++)
        {
            var bg = (row % 2 == 0) ? altBg : white;
            for (int c = 1; c <= total; c++)
            {
                ws.Cells[row, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(bg);
                ws.Cells[row, c].Style.Border.BorderAround(ExcelBorderStyle.Hair, cellBdr);
                ws.Cells[row, c].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            }
        }
        ws.Row(5).Height = 18;

        // Freeze top 4 rows
        ws.View.FreezePanes(5, 1);

        // ── Column widths ─────────────────────────────────────────
        var wideHeaders = new HashSet<string>
        {
            "FirstNameAr", "FatherNameAr", "GrandfatherNameAr", "FamilyNameAr",
            "FirstNameEn", "FatherNameEn", "GrandfatherNameEn", "FamilyNameEn",
        };
        for (int c = 1; c <= total; c++)
        {
            var col = selected[c - 1].Col;
            double width = requiredSet.Contains(col.Header) ? 20 : 17;
            if (wideHeaders.Contains(col.Header)) width = 18;
            else if (col.Header == "Email") width = 28;
            else if (col.Header is "EmployeeCode" or "InitialPassword") width = 18;
            ws.Column(c).Width = width;
        }

        // ── Column outline groups (collapse optional sections) ────
        // Section indices: 0=Basic, 1=Job, 2=Docs, 3=Personal, 4=Education, 5=Financial, 6=Contract.
        // Docs/Personal/Education/Contract collapsed by default; Financial visible-but-outlined.
        var collapsedSectionIdx = new HashSet<int> { 2, 3, 4, 6 };
        for (int c = 1; c <= total; c++)
        {
            int si = selected[c - 1].SecIdx;
            if (collapsedSectionIdx.Contains(si))
            {
                ws.Column(c).OutlineLevel = 1;
                ws.Column(c).Collapsed = true;
            }
            else if (si == 5)
            {
                ws.Column(c).OutlineLevel = 1;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SHEET 2 — مرجع
        // ══════════════════════════════════════════════════════════
        var wsRef = pkg.Workbook.Worksheets.Add("مرجع");
        wsRef.View.RightToLeft = true;

        void RefHeader(int row, int col, int toCol, string text, int r, int g, int b)
        {
            wsRef.Cells[row, col, row, toCol].Merge = true;
            wsRef.Cells[row, col].Value = text;
            using var c2 = wsRef.Cells[row, col, row, toCol];
            c2.Style.Font.Bold = true;
            c2.Style.Font.Color.SetColor(Color.White);
            c2.Style.Fill.PatternType = ExcelFillStyle.Solid;
            c2.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(r, g, b));
            c2.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            wsRef.Row(row).Height = 20;
        }

        // Page title
        wsRef.Cells[1, 1, 1, 8].Merge = true;
        wsRef.Cells[1, 1].Value = "ورقة المرجع — القيم المقبولة في قالب الاستيراد";
        wsRef.Cells[1, 1].Style.Font.Bold = true;
        wsRef.Cells[1, 1].Style.Font.Size = 12;
        wsRef.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        wsRef.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(15, 23, 42));
        wsRef.Cells[1, 1].Style.Font.Color.SetColor(Color.White);
        wsRef.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        wsRef.Row(1).Height = 26;

        int refRow = 3;

        // ── Branches ─────────────────────────────────────────────
        RefHeader(refRow, 1, 4, "الفروع المتاحة (استخدم قيمة معرف الفرع في عمود BranchSlug)", 26, 39, 68);
        refRow++;
        wsRef.Cells[refRow, 1].Value = "معرف الفرع (BranchSlug)";
        wsRef.Cells[refRow, 2].Value = "اسم الفرع";
        wsRef.Cells[refRow, 3].Value = "اسم الشركة";
        wsRef.Cells[refRow, 4].Value = "ملاحظة";
        using (var hdr = wsRef.Cells[refRow, 1, refRow, 4])
        {
            hdr.Style.Font.Bold = true;
            hdr.Style.Fill.PatternType = ExcelFillStyle.Solid;
            hdr.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 228, 245));
            hdr.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(150, 160, 190));
        }
        refRow++;
        foreach (var br in branches)
        {
            wsRef.Cells[refRow, 1].Value = br.Slug;
            wsRef.Cells[refRow, 2].Value = br.NameAr;
            wsRef.Cells[refRow, 3].Value = br.Company?.NameAr ?? "";
            wsRef.Cells[refRow, 4].Value = "انسخ القيمة من العمود الأول";
            wsRef.Cells[refRow, 1].Style.Font.Bold = true;
            wsRef.Cells[refRow, 1].Style.Font.Color.SetColor(Color.FromArgb(26, 39, 68));
            for (int c = 1; c <= 4; c++)
                wsRef.Cells[refRow, c].Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.FromArgb(200, 210, 230));
            refRow++;
        }
        refRow += 2;

        // ── Job Titles ─────────────────────────────────────────────
        RefHeader(refRow, 1, 3, "المسميات الوظيفية (أعمدة JobTitleName / ActualJobTitleName) — انسخ الاسم بالضبط", 91, 60, 20);
        refRow++;
        wsRef.Cells[refRow, 1].Value = "المسمى الوظيفي (انسخ هذا)";
        wsRef.Cells[refRow, 2].Value = "الشركة";
        wsRef.Cells[refRow, 3].Value = "ملاحظة";
        using (var hdr = wsRef.Cells[refRow, 1, refRow, 3])
        {
            hdr.Style.Font.Bold = true;
            hdr.Style.Fill.PatternType = ExcelFillStyle.Solid;
            hdr.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(250, 235, 215));
            hdr.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(190, 170, 140));
        }
        refRow++;
        foreach (var jtRef in jobTitlesForRef)
        {
            wsRef.Cells[refRow, 1].Value = jtRef.NameAr;
            wsRef.Cells[refRow, 2].Value = jtRef.Company?.NameAr ?? "";
            wsRef.Cells[refRow, 3].Value = "انسخ القيمة من العمود الأول";
            wsRef.Cells[refRow, 1].Style.Font.Bold = true;
            for (int c = 1; c <= 3; c++)
                wsRef.Cells[refRow, c].Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.FromArgb(220, 210, 190));
            refRow++;
        }
        refRow += 2;

        // ── Nationalities ─────────────────────────────────────────
        RefHeader(refRow, 1, 3, "الجنسيات (عمود Nationality) — انسخ الاسم العربي بالضبط", 13, 80, 55);
        refRow++;
        wsRef.Cells[refRow, 1].Value = "الجنسية (انسخ هذا)"; wsRef.Cells[refRow, 2].Value = "English"; wsRef.Cells[refRow, 3].Value = "الرمز الداخلي";
        using (var hdr = wsRef.Cells[refRow, 1, refRow, 3])
        {
            hdr.Style.Font.Bold = true;
            hdr.Style.Fill.PatternType = ExcelFillStyle.Solid;
            hdr.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(210, 240, 225));
        }
        refRow++;
        foreach (var n in Lookups.Nationalities)
        {
            wsRef.Cells[refRow, 1].Value = n.NameAr;
            wsRef.Cells[refRow, 2].Value = n.NameEn;
            wsRef.Cells[refRow, 3].Value = n.Code;
            wsRef.Cells[refRow, 1].Style.Font.Bold = true;
            refRow++;
        }
        refRow += 2;

        // ── Enum allowed values ───────────────────────────────────
        RefHeader(refRow, 1, 4, "القيم المقبولة للحقول المُقيّدة", 55, 65, 81);
        refRow++;
        var enumTable = new[]
        {
            ("الحالة الاجتماعية (MaritalStatus)", "Single / Married / Divorced / Widowed"),
            ("نوع الراتب (SalaryType)",           "Monthly / Daily / Contract"),
            ("نظام العمل (WorkSystem)",            "FullTime / PartTime / Remote"),
            ("نوع العقد (ContractType)",           "Limited / Unlimited"),
            ("يحمل مؤهل (IsEducated)",             "true / false"),
        };
        foreach (var (label, values) in enumTable)
        {
            wsRef.Cells[refRow, 1].Value = label;
            wsRef.Cells[refRow, 2].Value = values;
            wsRef.Cells[refRow, 1].Style.Font.Bold = true;
            using var hdr = wsRef.Cells[refRow, 1, refRow, 4];
            hdr.Style.Fill.PatternType = ExcelFillStyle.Solid;
            hdr.Style.Fill.BackgroundColor.SetColor(refRow % 2 == 0
                ? Color.FromArgb(245, 246, 250)
                : Color.White);
            for (int c = 1; c <= 4; c++)
                wsRef.Cells[refRow, c].Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.FromArgb(210, 215, 225));
            refRow++;
        }

        // Reference sheet widths
        wsRef.Column(1).Width = 30;
        wsRef.Column(2).Width = 28;
        wsRef.Column(3).Width = 22;
        wsRef.Column(4).Width = 22;

        // Embed the admin's required-column choice in the file itself (not just its styling), so
        // BulkImport POST can honor the exact same choice even if the file is filled and uploaded
        // in a completely different session/day.
        var embeddedRequired = string.Join(",", selected.Select(s => s.Col.Header).Where(h => requiredSet.Contains(h)));
        pkg.Workbook.Properties.SetCustomPropertyValue("HrRequiredColumns", embeddedRequired);

        var bytes = pkg.GetAsByteArray();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "employee-import-template.xlsx");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkImport(IFormFile? file)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();

        if (file == null || file.Length == 0)
        {
            TempData["err"] = "الرجاء اختيار ملف Excel للرفع.";
            return RedirectToAction(nameof(BulkImport));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
        {
            TempData["err"] = "يجب أن يكون الملف بصيغة .xlsx";
            return RedirectToAction(nameof(BulkImport));
        }

        var branches     = await _scope.Filter(_db.Branches.AsNoTracking()).ToListAsync();
        var jobTitles    = await _db.JobTitles.AsNoTracking().ToListAsync();
        var departments  = await _scope.Filter(_db.Departments.AsNoTracking()).ToListAsync();
        var sponsors     = await _scope.Filter(_db.Sponsors.AsNoTracking()).ToListAsync();
        var employees    = await _scope.Filter(_db.Employees.AsNoTracking()).ToListAsync();
        var usedCodes    = employees.Select(e => e.EmployeeCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedEmails   = employees.Select(e => e.Email).Where(e => e != null).ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        var result     = new BulkImportResultVM();
        var toCreate   = new List<(string email, string password, Employee employee)>();
        var seenCodes  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var stream = file.OpenReadStream();
        using var pkg = new ExcelPackage(stream);
        var ws = pkg.Workbook.Worksheets[0];

        int rowCount = ws.Dimension?.Rows ?? 1;
        // Auto-detect template version:
        //   New template (v2): row 3 col 1 contains Arabic label "رمز الموظف" → data starts at row 5
        //   Legacy template: row 1 col 1 is "EmployeeCode" → data starts at row 2
        bool isNewFormat   = ws.Cells[3, 1].Text.Trim() == "رمز الموظف";
        int  dataStartRow  = isNewFormat ? 5 : 2;

        // Header-based column lookup: the template may only contain a subset of the
        // full 69 columns (the admin picks which ones to export), so columns are matched
        // by their English header (row 4) instead of a fixed position.
        var headerCol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Which columns are mandatory is whatever the admin chose on the DownloadTemplate picker,
        // embedded in the file itself (see DownloadTemplate) — not a fixed list. Falls back to each
        // column's built-in default if the file has no embedded choice (e.g. hand-built file).
        // NOTE: check for null specifically, not IsNullOrEmpty — an *empty* embedded value is a real,
        // meaningful choice ("admin marked nothing as required"), not the same as "no choice was
        // embedded at all" (e.g. a hand-built file). Conflating the two was a bug: it silently fell
        // back to the static defaults whenever an admin deliberately cleared every Required checkbox.
        var embeddedRequired = pkg.Workbook.Properties.GetCustomPropertyValue("HrRequiredColumns") as string;
        var requiredSet = embeddedRequired is null
            ? EmployeeImportColumns.All.Where(c => c.Required).Select(c => c.Header).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(embeddedRequired.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

        if (isNewFormat)
        {
            int colCount = ws.Dimension?.Columns ?? 0;
            for (int c = 1; c <= colCount; c++)
            {
                var h = ws.Cells[4, c].Text.Trim();
                if (!string.IsNullOrEmpty(h) && !headerCol.ContainsKey(h)) headerCol[h] = c;
            }

            var missingRequired = EmployeeImportColumns.All
                .Where(c => requiredSet.Contains(c.Header) && !headerCol.ContainsKey(c.Header))
                .Select(c => c.NameAr)
                .ToList();
            if (missingRequired.Count > 0)
            {
                TempData["err"] = "الملف ينقصه أعمدة إلزامية: " + string.Join("، ", missingRequired);
                return RedirectToAction(nameof(BulkImport));
            }
        }

        for (int row = dataStartRow; row <= rowCount; row++)
        {
            string S(string header) => headerCol.TryGetValue(header, out var c) ? ws.Cells[row, c].Text.Trim() : "";
            DateOnly? D(string header)
            {
                if (!headerCol.TryGetValue(header, out var c)) return null;
                var cell = ws.Cells[row, c];
                if (cell.Value is DateTime dt) return DateOnly.FromDateTime(dt);
                var text = cell.Text.Trim();
                return DateOnly.TryParse(text, out var d) ? d : null;
            }
            decimal? Dec(string header) => decimal.TryParse(S(header), out var d) ? d : null;
            int? I(string header) => int.TryParse(S(header), out var n) ? n : null;
            bool B(string header) => S(header) is "true" or "1" or "yes" or "نعم";
            TEnum? E<TEnum>(string header) where TEnum : struct, Enum =>
                Enum.TryParse<TEnum>(S(header), true, out var v) ? v : null;

            var code   = S("EmployeeCode");
            var email  = S("Email");
            var pass   = S("InitialPassword");
            var firstNameAr = S("FirstNameAr");
            var fatherNameAr = S("FatherNameAr");
            var grandfatherNameAr = S("GrandfatherNameAr");
            var familyNameAr = S("FamilyNameAr");
            var brSlug = S("BranchSlug");
            var deptName = S("DepartmentName");
            var sponsorName = S("SponsorName");
            var nationalityName = S("Nationality");
            var jtName = S("JobTitleName");
            var actualJtName = S("ActualJobTitleName");

            // Skip truly empty rows
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(firstNameAr) && string.IsNullOrEmpty(email))
                continue;

            result.TotalRows++;
            var errors = new List<string>();

            // Required-ness is whatever the admin chose for this template (requiredSet above),
            // not a fixed field list — any column can be made mandatory or optional.
            foreach (var reqCol in EmployeeImportColumns.All)
            {
                if (!requiredSet.Contains(reqCol.Header)) continue;
                if (string.IsNullOrEmpty(S(reqCol.Header)))
                    errors.Add($"{reqCol.NameAr} مطلوب");
            }
            // Independent of required-ness: a password that WAS provided must still meet the minimum length.
            if (pass.Length is > 0 and < 6) errors.Add("كلمة المرور يجب أن تكون 6 أحرف على الأقل");

            if (errors.Count == 0 && usedCodes.Contains(code))   errors.Add($"رمز الموظف '{code}' مستخدم مسبقاً");
            if (errors.Count == 0 && seenCodes.Contains(code))   errors.Add($"رمز الموظف '{code}' مكرر في الملف");
            if (errors.Count == 0 && usedEmails.Contains(email)) errors.Add($"البريد '{email}' مستخدم مسبقاً");
            if (errors.Count == 0 && seenEmails.Contains(email)) errors.Add($"البريد '{email}' مكرر في الملف");

            Branch? branch = null;
            if (errors.Count == 0)
            {
                branch = branches.FirstOrDefault(b => b.Slug.Equals(brSlug, StringComparison.OrdinalIgnoreCase));
                if (branch == null) errors.Add($"لا يوجد فرع بالمعرف '{brSlug}'");
            }

            JobTitle? jt = null;
            if (errors.Count == 0 && !string.IsNullOrEmpty(jtName))
            {
                jt = jobTitles.FirstOrDefault(j =>
                    j.CompanyId == branch!.CompanyId &&
                    j.NameAr.Equals(jtName, StringComparison.OrdinalIgnoreCase));
                if (jt == null) errors.Add($"لا يوجد مسمى وظيفي بالاسم '{jtName}' في شركة الفرع");
            }

            JobTitle? actualJt = null;
            if (errors.Count == 0 && !string.IsNullOrEmpty(actualJtName))
            {
                actualJt = jobTitles.FirstOrDefault(j =>
                    j.CompanyId == branch!.CompanyId &&
                    j.NameAr.Equals(actualJtName, StringComparison.OrdinalIgnoreCase));
                if (actualJt == null) errors.Add($"لا يوجد مسمى وظيفي فعلي بالاسم '{actualJtName}' في شركة الفرع");
            }

            Department? dept = null;
            if (errors.Count == 0 && !string.IsNullOrEmpty(deptName))
            {
                dept = departments.FirstOrDefault(d =>
                    d.CompanyId == branch!.CompanyId && d.NameAr.Equals(deptName, StringComparison.OrdinalIgnoreCase));
                if (dept == null) errors.Add($"لا توجد إدارة بالاسم '{deptName}' في شركة الفرع");
            }

            Sponsor? sponsor = null;
            if (errors.Count == 0 && !string.IsNullOrEmpty(sponsorName))
            {
                sponsor = sponsors.FirstOrDefault(s =>
                    s.CompanyId == branch!.CompanyId && s.NameAr.Equals(sponsorName, StringComparison.OrdinalIgnoreCase));
                if (sponsor == null) errors.Add($"لا يوجد كفيل بالاسم '{sponsorName}' في شركة الفرع");
            }

            // Nationality is typed as the full Arabic name (see Reference sheet), not a code.
            // Blank stays blank — no default nationality is assumed. A non-blank name that doesn't
            // match anything in the list is a validation error, same as DepartmentName/SponsorName.
            var nationality = string.IsNullOrEmpty(nationalityName)
                ? null
                : Lookups.Nationalities.FirstOrDefault(n => n.NameAr.Equals(nationalityName, StringComparison.OrdinalIgnoreCase));
            if (errors.Count == 0 && !string.IsNullOrEmpty(nationalityName) && nationality == null)
                errors.Add($"لا توجد جنسية بالاسم '{nationalityName}'");

            if (errors.Count > 0)
            {
                result.Errors.Add(new BulkImportErrorRow
                {
                    RowNumber    = row,
                    EmployeeCode = code,
                    Email        = email,
                    Error        = string.Join(" · ", errors)
                });
                continue;
            }

            seenCodes.Add(code);
            seenEmails.Add(email);

            var nationalityCode = nationality?.Code;
            var isSaudi = nationalityCode == "SA";
            var nameAr = string.Join(" ", new[] { firstNameAr, fatherNameAr, grandfatherNameAr, familyNameAr }.Where(p => !string.IsNullOrWhiteSpace(p)));
            var nameEnParts = new[] { S("FirstNameEn"), S("FatherNameEn"), S("GrandfatherNameEn"), S("FamilyNameEn") }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            var nameEn = nameEnParts.Count > 0 ? string.Join(" ", nameEnParts) : nameAr;
            var isEducated = B("IsEducated");
            var supervisorCode = S("DirectSupervisorCode");
            var supervisor = !string.IsNullOrEmpty(supervisorCode)
                ? employees.FirstOrDefault(e => e.EmployeeCode.Equals(supervisorCode, StringComparison.OrdinalIgnoreCase))
                : null;

            var employee = new Employee
            {
                EmployeeCode = code, Email = email, Status = "active", AvatarColor = "#1a2744",
                NameAr = nameAr, NameEn = nameEn, Initials = ComputeInitials(nameAr),
                FirstNameAr = firstNameAr, FatherNameAr = fatherNameAr, GrandfatherNameAr = grandfatherNameAr, FamilyNameAr = familyNameAr,
                FirstNameEn = S("FirstNameEn"), FatherNameEn = S("FatherNameEn"), GrandfatherNameEn = S("GrandfatherNameEn"), FamilyNameEn = S("FamilyNameEn"),
                CompanyId = branch!.CompanyId, BranchId = branch.Id, JobTitleId = jt?.Id,
                DepartmentId = dept?.Id, SponsorId = sponsor?.Id,
                CostCenter = S("CostCenter"), HireDate = D("HireDate"), LastWorkDate = D("LastWorkDate"), BasicSalary = Dec("BasicSalary") ?? 0, Phone = S("Phone"),
                NationalId = S("NationalId"), NationalIdExpiry = D("NationalIdExpiry"), IqamaNumber = S("IqamaNumber"), IqamaExpiry = D("IqamaExpiry"),
                PassportNumber = S("PassportNumber"), PassportExpiry = D("PassportExpiry"),
                NationalityCode = nationalityCode, NationalityAr = nationality?.NameAr, NationalityEn = nationality?.NameEn,
                CreatedByUserId = _users.GetUserId(User), CreatedAt = DateTime.UtcNow,
                Personal = new EmployeePersonal
                {
                    MaritalStatus = E<MaritalStatus>("MaritalStatus"), ChildrenCount = I("ChildrenCount"), BloodType = S("BloodType"),
                    City = S("City"), District = S("District"), Street = S("Street"),
                    EmergencyContactName = S("EmergencyContactName"), EmergencyContactPhone = S("EmergencyContactPhone"),
                    IsEducated = isEducated,
                    EducationLevel       = isEducated ? S("EducationLevel") : null,
                    EducationMajor       = isEducated ? S("EducationMajor") : null,
                    EducationInstitution = isEducated ? S("EducationInstitution") : null,
                    EducationCountry     = isEducated ? S("EducationCountry") : null,
                    GraduationYear       = isEducated ? I("GraduationYear") : null,
                    Gpa                  = isEducated ? Dec("Gpa") : null,
                    CertificateNumber    = isEducated ? S("CertificateNumber") : null,
                },
                Financial = new EmployeeFinancial
                {
                    BankName = S("BankName"), Iban = S("Iban"), BeneficiaryName = S("BeneficiaryName"),
                    HousingAllowance = Dec("HousingAllowance") ?? 0, TransportAllowance = Dec("TransportAllowance") ?? 0, OtherAllowance = Dec("OtherAllowance") ?? 0,
                    OtherAllowanceDescription = S("OtherAllowanceDescription"),
                    EndOfServiceAdvance = isSaudi ? null : Dec("EndOfServiceAdvance"),
                    SalaryType = E<SalaryType>("SalaryType") ?? SalaryType.Monthly,
                    DailyRate = Dec("DailyRate"), ContractTotalValue = Dec("ContractTotalValue"), ContractDurationMonths = I("FinancialContractDurationMonths"),
                },
                Work = new EmployeeWork
                {
                    ActualJobTitleId = actualJt?.Id, DirectSupervisorId = supervisor?.Id,
                    WorkLocation = S("WorkLocation"), WorkSystem = E<WorkSystem>("WorkSystem") ?? WorkSystem.FullTime, DailyWorkHours = Dec("DailyWorkHours"),
                    ContractType = E<ContractType>("ContractType") ?? ContractType.Unlimited,
                    ContractDurationMonths = I("ContractDurationMonths"), ProbationDays = I("ProbationDays") ?? 90,
                    AnnualLeaveBalance = I("AnnualLeaveBalance") ?? 0, LastLeaveDate = D("LastLeaveDate"), SickLeaveUsedDays = I("SickLeaveUsedDays") ?? 0,
                },
            };
            employee.ContractStartDate = D("ContractStartDate");
            employee.ContractEndDate   = D("ContractEndDate");

            toCreate.Add((email, pass, employee));
        }

        // Create Identity users + Employee rows for valid entries
        foreach (var (email, pass, employee) in toCreate)
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var createResult = await _users.CreateAsync(user, pass);
            if (!createResult.Succeeded)
            {
                result.Errors.Add(new BulkImportErrorRow
                {
                    RowNumber    = 0,
                    EmployeeCode = employee.EmployeeCode,
                    Email        = email,
                    Error        = string.Join("; ", createResult.Errors.Select(e => e.Description))
                });
                continue;
            }
            employee.UserId = user.Id;
            _db.Employees.Add(employee);
            result.Imported++;
        }

        if (result.Imported > 0)
        {
            await _db.SaveChangesAsync();
            await _activity.LogAsync("Employee", "bulk-import", "BulkImport",
                userId: _users.GetUserId(User));
        }

        TempData["BulkImportResult"] = JsonSerializer.Serialize(result);
        return RedirectToAction(nameof(BulkImportResult));
    }

    [HttpGet]
    public IActionResult BulkImportResult()
    {
        if (TempData["BulkImportResult"] is not string json)
            return RedirectToAction(nameof(Index));
        var result = JsonSerializer.Deserialize<BulkImportResultVM>(json) ?? new BulkImportResultVM();
        ViewData["Title"] = "نتيجة الاستيراد";
        ViewData["CurrentServiceId"] = CreateSvc;
        return View(result);
    }

    // ─── Import the company's own employee sheet (no template) ─────────────
    // Two steps: ImportSheetPreview (dry run, nothing saved) → ImportSheetCommit (saves in one transaction).
    // The uploaded file is kept in a temp folder between the two steps, keyed by a random token.
    // See docs/features/employee-sheet-import.md.

    private static string ImportTempDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "hr-sheet-import");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static bool ValidToken(string? token) =>
        !string.IsNullOrEmpty(token) && System.Text.RegularExpressions.Regex.IsMatch(token, "^[a-f0-9]{32}$");

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ImportSheetPreview(IFormFile? file, SheetImportMode mode = SheetImportMode.SkipExisting)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        if (file == null || file.Length == 0 || Path.GetExtension(file.FileName).ToLowerInvariant() != ".xlsx")
        {
            TempData["err"] = "الرجاء اختيار ملف كشف الموظفين بصيغة .xlsx";
            return RedirectToAction(nameof(BulkImport));
        }

        // Housekeeping: drop leftovers older than a day.
        foreach (var old in Directory.GetFiles(ImportTempDir).Where(f => System.IO.File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-1)))
            try { System.IO.File.Delete(old); } catch { /* ignore */ }

        var token = Guid.NewGuid().ToString("N");
        var path = Path.Combine(ImportTempDir, token + ".xlsx");
        await using (var fs = System.IO.File.Create(path)) await file.CopyToAsync(fs);

        SheetImportReport report;
        try
        {
            await using var fs = System.IO.File.OpenRead(path);
            report = await new EmployeeSheetImporter(_db).RunAsync(fs, file.FileName, mode, commit: false,
                _scope.Current.CompanyId, _scope.Current.BranchId, _users.GetUserId(User));
        }
        catch (Exception)
        {
            TempData["err"] = "تعذّر قراءة الملف — تأكد أنه ملف Excel سليم (.xlsx) وغير محمي بكلمة مرور.";
            return RedirectToAction(nameof(BulkImport));
        }
        report.Token = token;
        await System.IO.File.WriteAllTextAsync(Path.Combine(ImportTempDir, token + ".json"), JsonSerializer.Serialize(report));

        ViewData["Title"] = "مراجعة استيراد كشف الموظفين";
        ViewData["CurrentServiceId"] = CreateSvc;
        return View("ImportSheetPreview", report);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportSheetCommit(string token, SheetImportMode mode)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        var path = ValidToken(token) ? Path.Combine(ImportTempDir, token + ".xlsx") : null;
        if (path == null || !System.IO.File.Exists(path))
        {
            TempData["err"] = "انتهت صلاحية المعاينة أو تم الاستيراد مسبقاً — ارفع الملف من جديد.";
            return RedirectToAction(nameof(BulkImport));
        }

        var original = System.IO.File.Exists(Path.Combine(ImportTempDir, token + ".json"))
            ? JsonSerializer.Deserialize<SheetImportReport>(await System.IO.File.ReadAllTextAsync(Path.Combine(ImportTempDir, token + ".json")))
            : null;

        SheetImportReport report;
        try
        {
            await using var fs = System.IO.File.OpenRead(path);
            report = await new EmployeeSheetImporter(_db).RunAsync(fs, original?.FileName ?? "employees.xlsx", mode, commit: true,
                _scope.Current.CompanyId, _scope.Current.BranchId, _users.GetUserId(User));
        }
        catch (Exception ex)
        {
            // The importer saves everything in one transaction, so a failure here means nothing was written.
            TempData["err"] = "لم يُحفظ أي شيء — حدث خطأ أثناء الحفظ: " + (ex.InnerException?.Message ?? ex.Message);
            return RedirectToAction(nameof(BulkImport));
        }
        System.IO.File.Delete(path);   // one-shot: the same preview can't be committed twice
        report.Token = token;
        await System.IO.File.WriteAllTextAsync(Path.Combine(ImportTempDir, token + ".json"), JsonSerializer.Serialize(report));

        await _activity.LogAsync("Employee", "sheet-import", "SheetImport", userId: _users.GetUserId(User),
            details: $"{report.FileName}: جديد {report.ToCreate} · تحديث {report.ToUpdate} · متخطى {report.SkippedExisting} · أخطاء {report.ErrorRows}");

        ViewData["Title"] = "نتيجة استيراد كشف الموظفين";
        ViewData["CurrentServiceId"] = CreateSvc;
        return View("ImportSheetPreview", report);
    }

    // Excel list of every error/warning so HR can fix the source data.
    [HttpGet]
    public async Task<IActionResult> ImportSheetIssues(string token)
    {
        if (!await _perms.CanAsync(User, CreateSvc, HrBusinessPermissions.Create, _scope.Current)) return Forbid();
        var jsonPath = ValidToken(token) ? Path.Combine(ImportTempDir, token + ".json") : null;
        if (jsonPath == null || !System.IO.File.Exists(jsonPath)) return NotFound();
        var report = JsonSerializer.Deserialize<SheetImportReport>(await System.IO.File.ReadAllTextAsync(jsonPath))!;

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("ملاحظات الاستيراد");
        ws.View.RightToLeft = true;
        string[] headers = { "رقم الصف في الملف", "رقم الموظف", "اسم الموظف", "النوع", "الملاحظة" };
        for (int c = 0; c < headers.Length; c++) ws.Cells[1, c + 1].Value = headers[c];
        using (var h = ws.Cells[1, 1, 1, headers.Length])
        {
            h.Style.Font.Bold = true; h.Style.Font.Color.SetColor(Color.White);
            h.Style.Fill.PatternType = ExcelFillStyle.Solid; h.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(26, 39, 68));
        }
        int row = 2;
        foreach (var i in report.Issues.OrderBy(i => i.Level == "error" ? 0 : 1).ThenBy(i => i.Row))
        {
            ws.Cells[row, 1].Value = i.Row; ws.Cells[row, 2].Value = i.Code; ws.Cells[row, 3].Value = i.Name;
            ws.Cells[row, 4].Value = i.Level switch
            {
                "error"     => "خطأ — لم يُستورد",
                "duplicate" => "مكرر — دُمج مع الصف الأكمل",
                _           => "تنبيه — استورد ويحتاج مراجعة",
            };
            ws.Cells[row, 5].Value = i.Message;
            ws.Cells[row, 4].Style.Font.Color.SetColor(i.Level == "error" ? Color.FromArgb(185, 28, 28) : Color.FromArgb(180, 83, 9));
            row++;
        }
        ws.Cells[1, 1, Math.Max(row - 1, 1), headers.Length].AutoFilter = true;
        ws.View.FreezePanes(2, 1);
        ws.Column(1).Width = 12; ws.Column(2).Width = 12; ws.Column(3).Width = 32; ws.Column(4).Width = 26; ws.Column(5).Width = 90;
        return File(pkg.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ملاحظات-استيراد-الموظفين-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    // ─── Print Roster ─────────────────────────────────────────────────────
    // See docs/features/employee-roster-print.md

    [HttpGet]
    public async Task<IActionResult> PrintSetup()
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();

        var companiesQuery = _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).AsQueryable();
        if (_scope.Current.CompanyId.HasValue)
            companiesQuery = companiesQuery.Where(c => c.Id == _scope.Current.CompanyId.Value);

        // Live head-counts per branch (inside the active scope) so every scope row can show how many employees it holds.
        var branchCounts = await _scope.Filter(_db.Employees.AsNoTracking())
            .GroupBy(e => new { e.CompanyId, e.BranchId })
            .Select(g => new { g.Key.CompanyId, g.Key.BranchId, Count = g.Count() })
            .ToListAsync();

        var vm = new PrintRosterSetupVM
        {
            Companies = await companiesQuery.ToListAsync(),
            Branches = await _scope.Filter(_db.Branches.AsNoTracking())
                .Include(b => b.Company)
                .OrderBy(b => b.CompanyId).ThenBy(b => b.NameAr)
                .ToListAsync(),
            Departments = await _scope.Filter(_db.Departments.AsNoTracking()).OrderBy(d => d.NameAr).ToListAsync(),
            JobTitles = await _scope.Filter(_db.JobTitles.AsNoTracking()).OrderBy(j => j.NameAr).ToListAsync(),
            Sponsors = await _scope.Filter(_db.Sponsors.AsNoTracking()).OrderBy(s => s.NameAr).ToListAsync(),
            Nationalities = Lookups.Nationalities,
            BranchEmployeeCounts = branchCounts.GroupBy(x => x.BranchId).ToDictionary(g => g.Key, g => g.Sum(x => x.Count)),
            CompanyEmployeeCounts = branchCounts.GroupBy(x => x.CompanyId).ToDictionary(g => g.Key, g => g.Sum(x => x.Count)),
            TotalEmployees = branchCounts.Sum(x => x.Count),
        };
        ViewData["Title"] = _l.L("طباعة كشف الموظفين", "Print Employee Roster");
        ViewData["CurrentServiceId"] = ListSvc;
        return View(vm);
    }

    // Live summary for the setup page (count + first rows) — same query as the preview/PDF/Excel.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintSummary(PrintRosterRequestVM m)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();

        var (employees, columns) = await ResolveRosterAsync(m);
        return Json(new
        {
            count = employees.Count,
            headers = columns.Select(c => c.NameAr),
            keys = columns.Select(c => c.Header),
            rows = employees.Take(8).Select(e => columns.Select(c => EmployeeColumnReader.Readers[c.Header](e))),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintPreview(PrintRosterRequestVM m)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();

        var (employees, columns) = await ResolveRosterAsync(m);

        var vm = new PrintRosterResultVM
        {
            ColumnHeaders = columns.Select(c => c.NameAr).ToList(),
            ColumnKeys = columns.Select(c => c.Header).ToList(),
            Rows = employees.Select(e => columns.Select(c => EmployeeColumnReader.Readers[c.Header](e)).ToArray()).ToList(),
            EmployeeCount = employees.Count,
            ScopeSummary = await BuildScopeSummaryAsync(m.CompanyIds, m.BranchIds),
            FilterSummary = await BuildFilterSummaryAsync(m),
            GeneratedBy = User.Identity?.Name ?? "",
            Request = m,
            CompanyIds = m.CompanyIds ?? Array.Empty<int>(),
            BranchIds = m.BranchIds ?? Array.Empty<int>(),
            Columns = columns.Select(c => c.Header).ToList(),
        };
        ViewData["Title"] = _l.L("مراجعة كشف الموظفين", "Review Employee Roster");
        ViewData["CurrentServiceId"] = ListSvc;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintPdf(PrintRosterRequestVM m)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();

        var (employees, columns) = await ResolveRosterAsync(m);

        var data = new RosterPdfData
        {
            ScopeSummary = await BuildScopeSummaryAsync(m.CompanyIds, m.BranchIds),
            FilterSummary = m.ShowFilters ? string.Join("  ·  ", await BuildFilterSummaryAsync(m)) : "",
            Portrait = m.Orientation == "portrait",
            RowNumbers = m.RowNumbers,
            Headers = columns.Select(c => c.NameAr).ToList(),
            Rows = employees.Select(e => columns.Select(c => EmployeeColumnReader.Readers[c.Header](e)).ToArray()).ToList(),
        };
        var pdf = _rosterPdf.Build(data);

        await _activity.LogAsync("Employee", "roster-print", "PrintPdf", userId: _users.GetUserId(User));

        return File(pdf, "application/pdf", $"employee-roster-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintExcel(PrintRosterRequestVM m)
    {
        if (!await _perms.CanAsync(User, ListSvc, HrBusinessPermissions.View, _scope.Current)) return Forbid();

        var (employees, columns) = await ResolveRosterAsync(m);
        var scope = await BuildScopeSummaryAsync(m.CompanyIds, m.BranchIds);
        var filters = await BuildFilterSummaryAsync(m);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("كشف الموظفين");
        ws.View.RightToLeft = true;

        var navy = Color.FromArgb(26, 39, 68);
        var gold = Color.FromArgb(200, 168, 75);
        var offset = m.RowNumbers ? 1 : 0;
        var total = columns.Count + offset;

        // Title block (rows 1-3), then the table header on row 5.
        ws.Cells[1, 1].Value = _l.L("كشف الموظفين", "Employee Roster");
        ws.Cells[1, 1, 1, total].Merge = true;
        ws.Cells[1, 1].Style.Font.Size = 16;
        ws.Cells[1, 1].Style.Font.Bold = true;
        ws.Cells[1, 1].Style.Font.Color.SetColor(navy);
        ws.Cells[2, 1].Value = $"{scope} — {employees.Count} {_l.L("موظف", "employees")} — {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cells[2, 1, 2, total].Merge = true;
        ws.Cells[2, 1].Style.Font.Color.SetColor(Color.FromArgb(90, 100, 120));
        if (m.ShowFilters && filters.Count > 0)
        {
            ws.Cells[3, 1].Value = string.Join("  ·  ", filters);
            ws.Cells[3, 1, 3, total].Merge = true;
            ws.Cells[3, 1].Style.Font.Color.SetColor(Color.FromArgb(90, 100, 120));
        }

        const int headRow = 5;
        if (m.RowNumbers) ws.Cells[headRow, 1].Value = "#";
        for (int c = 0; c < columns.Count; c++) ws.Cells[headRow, c + 1 + offset].Value = columns[c].NameAr;
        using (var r = ws.Cells[headRow, 1, headRow, total])
        {
            r.Style.Font.Bold = true;
            r.Style.Font.Color.SetColor(Color.White);
            r.Style.Fill.PatternType = ExcelFillStyle.Solid;
            r.Style.Fill.BackgroundColor.SetColor(navy);
            r.Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
            r.Style.Border.Bottom.Color.SetColor(gold);
            r.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            r.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }
        ws.Row(headRow).Height = 24;

        for (int i = 0; i < employees.Count; i++)
        {
            var row = headRow + 1 + i;
            if (m.RowNumbers) ws.Cells[row, 1].Value = i + 1;
            for (int c = 0; c < columns.Count; c++)
                ws.Cells[row, c + 1 + offset].Value = EmployeeColumnReader.Readers[columns[c].Header](employees[i]);
            if (i % 2 == 1)
            {
                ws.Cells[row, 1, row, total].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 1, row, total].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(245, 247, 250));
            }
        }

        var lastRow = headRow + Math.Max(employees.Count, 1);
        ws.Cells[headRow, 1, lastRow, total].AutoFilter = true;
        ws.View.FreezePanes(headRow + 1, 1);
        ws.Cells[headRow, 1, lastRow, total].AutoFitColumns(8, 45);
        ws.PrinterSettings.Orientation = m.Orientation == "portrait" ? eOrientation.Portrait : eOrientation.Landscape;
        ws.PrinterSettings.FitToPage = true;
        ws.PrinterSettings.FitToWidth = 1;
        ws.PrinterSettings.FitToHeight = 0;
        ws.PrinterSettings.RepeatRows = ws.Cells[$"{headRow}:{headRow}"];

        await _activity.LogAsync("Employee", "roster-print", "PrintExcel", userId: _users.GetUserId(User));

        return File(pkg.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"employee-roster-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    // Shared by PrintSummary/PrintPreview/PrintPdf/PrintExcel so all outputs always reflect the exact same
    // live data + column set. Scope can only narrow the active IScopeContext, never widen it.
    private async Task<(List<Employee> Employees, List<EmployeeImportColumn> Columns)> ResolveRosterAsync(PrintRosterRequestVM m)
    {
        var query = _scope.Filter(_db.Employees.AsNoTracking())
            .Include(e => e.Branch)
            .Include(e => e.JobTitle)
            .Include(e => e.Department)
            .Include(e => e.Sponsor)
            .Include(e => e.Personal)
            .Include(e => e.Financial)
            .Include(e => e.Work).ThenInclude(w => w!.ActualJobTitle)
            .Include(e => e.Work).ThenInclude(w => w!.DirectSupervisor)
            .AsQueryable();

        if (m.CompanyIds is { Length: > 0 }) query = query.Where(e => m.CompanyIds.Contains(e.CompanyId));
        if (m.BranchIds is { Length: > 0 }) query = query.Where(e => m.BranchIds.Contains(e.BranchId));

        query = ApplyRosterFilters(query, m);

        var employees = await query.OrderBy(e => e.NameAr).ToListAsync();

        // Keep the order the user arranged in "ترتيب الطباعة"; unknown/duplicate keys are dropped.
        var printable = EmployeeImportColumns.All
            .Where(c => c.Header != "InitialPassword")
            .Concat(EmployeeColumnReader.ExtraColumns)
            .ToDictionary(c => c.Header, StringComparer.OrdinalIgnoreCase);
        var columns = (m.Columns ?? new List<string>())
            .Where(printable.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(h => printable[h])
            .ToList();
        // Nothing selected (e.g. tampered/empty submission) => fall back to everything printable.
        if (columns.Count == 0)
            columns = printable.Values.ToList();

        return (employees, columns);
    }

    private static IQueryable<Employee> ApplyRosterFilters(IQueryable<Employee> query, PrintRosterRequestVM m)
    {
        if (!string.IsNullOrWhiteSpace(m.Q))
        {
            var q = m.Q.Trim();
            query = query.Where(e =>
                e.NameAr.Contains(q) || e.NameEn.Contains(q) || e.EmployeeCode.Contains(q) ||
                (e.NationalId != null && e.NationalId.Contains(q)) ||
                (e.IqamaNumber != null && e.IqamaNumber.Contains(q)) ||
                (e.Phone != null && e.Phone.Contains(q)));
        }

        if (EmployeeStatuses.IsValid(m.Status))
            query = query.Where(e => e.Status == m.Status);

        switch (m.Nationality)
        {
            case null or "": break;
            case "SA":      query = query.Where(e => e.NationalityCode == "SA"); break;
            case "NON_SA":  query = query.Where(e => e.NationalityCode != "SA"); break;
            default:        query = query.Where(e => e.NationalityCode == m.Nationality); break;
        }

        if (m.DepartmentId.HasValue) query = query.Where(e => e.DepartmentId == m.DepartmentId);
        if (m.JobTitleId.HasValue)   query = query.Where(e => e.JobTitleId == m.JobTitleId);
        if (m.SponsorId.HasValue)    query = query.Where(e => e.SponsorId == m.SponsorId);
        if (m.HiredFrom.HasValue)    query = query.Where(e => e.HireDate >= m.HiredFrom);
        if (m.HiredTo.HasValue)      query = query.Where(e => e.HireDate <= m.HiredTo);

        if (Enum.TryParse<ContractType>(m.ContractType, out var ct))
            query = query.Where(e => e.Work != null && e.Work.ContractType == ct);

        if (m.DocExpiryDays.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (m.DocExpiryDays.Value <= 0)
                query = query.Where(e => (e.IqamaExpiry != null && e.IqamaExpiry < today) ||
                                         (e.NationalIdExpiry != null && e.NationalIdExpiry < today));
            else
            {
                var until = today.AddDays(m.DocExpiryDays.Value);
                query = query.Where(e => (e.IqamaExpiry != null && e.IqamaExpiry >= today && e.IqamaExpiry <= until) ||
                                         (e.NationalIdExpiry != null && e.NationalIdExpiry >= today && e.NationalIdExpiry <= until));
            }
        }
        return query;
    }

    // Human-readable list of the active filters (shown on the preview, PDF and Excel).
    private async Task<List<string>> BuildFilterSummaryAsync(PrintRosterRequestVM m)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.Q)) list.Add(_l.L("بحث: ", "Search: ") + m.Q.Trim());
        if (!string.IsNullOrEmpty(m.Status))
            list.Add(_l.L("الحالة: ", "Status: ") + _l.L(EmployeeStatuses.Get(m.Status).NameAr, EmployeeStatuses.Get(m.Status).NameEn));
        if (!string.IsNullOrEmpty(m.Nationality))
            list.Add(_l.L("الجنسية: ", "Nationality: ") + m.Nationality switch
            {
                "SA" => _l.L("سعودي", "Saudi"),
                "NON_SA" => _l.L("غير سعودي", "Non-Saudi"),
                _ => Lookups.Nationalities.FirstOrDefault(n => n.Code == m.Nationality) is { } n ? _l.L(n.NameAr, n.NameEn) : m.Nationality,
            });
        if (m.DepartmentId.HasValue)
            list.Add(_l.L("الإدارة: ", "Department: ") + await _db.Departments.AsNoTracking().Where(d => d.Id == m.DepartmentId).Select(d => d.NameAr).FirstOrDefaultAsync());
        if (m.JobTitleId.HasValue)
            list.Add(_l.L("المسمى: ", "Job title: ") + await _db.JobTitles.AsNoTracking().Where(j => j.Id == m.JobTitleId).Select(j => j.NameAr).FirstOrDefaultAsync());
        if (m.SponsorId.HasValue)
            list.Add(_l.L("الكفيل: ", "Sponsor: ") + await _db.Sponsors.AsNoTracking().Where(s => s.Id == m.SponsorId).Select(s => s.NameAr).FirstOrDefaultAsync());
        if (m.HiredFrom.HasValue || m.HiredTo.HasValue)
            list.Add(_l.L("التعيين: ", "Hired: ") + $"{m.HiredFrom?.ToString("yyyy-MM-dd") ?? "…"} → {m.HiredTo?.ToString("yyyy-MM-dd") ?? "…"}");
        if (Enum.TryParse<ContractType>(m.ContractType, out var ct))
            list.Add(_l.L("العقد: ", "Contract: ") + (ct == ContractType.Limited ? _l.L("محدد المدة", "Limited") : _l.L("غير محدد المدة", "Unlimited")));
        if (m.DocExpiryDays.HasValue)
            list.Add(m.DocExpiryDays.Value <= 0
                ? _l.L("الهوية/الإقامة: منتهية", "ID/Iqama: expired")
                : _l.L($"الهوية/الإقامة تنتهي خلال {m.DocExpiryDays} يوماً", $"ID/Iqama expires within {m.DocExpiryDays} days"));
        return list;
    }

    private async Task<string> BuildScopeSummaryAsync(int[]? companyIds, int[]? branchIds)
    {
        var hasCompanies = companyIds is { Length: > 0 };
        var hasBranches = branchIds is { Length: > 0 };
        if (!hasCompanies && !hasBranches)
            return _l.L("جميع الشركات والفروع ضمن نطاق صلاحيتك الحالي", "All companies/branches within your current scope");

        var parts = new List<string>();
        if (hasCompanies)
        {
            var names = await _db.Companies.AsNoTracking()
                .Where(c => companyIds!.Contains(c.Id)).Select(c => c.NameAr).ToListAsync();
            parts.Add(_l.L("الشركات: ", "Companies: ") + string.Join("، ", names));
        }
        if (hasBranches)
        {
            var names = await _db.Branches.AsNoTracking()
                .Where(b => branchIds!.Contains(b.Id)).Select(b => b.NameAr).ToListAsync();
            parts.Add(_l.L("الفروع: ", "Branches: ") + string.Join("، ", names));
        }
        return string.Join(" — ", parts);
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    // Password rules come from Program.cs (AddIdentity options): 8+ chars, a digit, an uppercase letter.
    private static string TranslateIdentityError(IdentityError e, string? email) => e.Code switch
    {
        "DuplicateEmail"                  => $"البريد الإلكتروني '{email}' مستخدم مسبقاً.",
        "DuplicateUserName"               => $"اسم المستخدم '{email}' مستخدم مسبقاً.",
        "InvalidEmail"                    => $"البريد الإلكتروني '{email}' غير صالح.",
        "InvalidUserName"                 => $"اسم المستخدم '{email}' غير صالح.",
        "PasswordTooShort"                => "كلمة المرور قصيرة — يجب أن تكون 8 أحرف على الأقل.",
        "PasswordRequiresNonAlphanumeric" => "كلمة المرور يجب أن تحتوي على رمز خاص (!@#...).",
        "PasswordRequiresDigit"           => "كلمة المرور يجب أن تحتوي على رقم واحد على الأقل.",
        "PasswordRequiresUpper"           => "كلمة المرور يجب أن تحتوي على حرف إنجليزي كبير (A-Z).",
        "PasswordRequiresLower"           => "كلمة المرور يجب أن تحتوي على حرف إنجليزي صغير (a-z).",
        _                                 => e.Description,
    };

    private static string ComputeInitials(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr)) return "؟؟";
        var parts = nameAr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "؟؟";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)];
        return $"{parts[0][..1]}{parts[1][..1]}";
    }
}
