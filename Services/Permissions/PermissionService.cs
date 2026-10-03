using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Services.Permissions;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    // Per-request cache. Service is registered scoped, so the cache lives one request.
    private UserKind? _cachedKind;
    private string? _cachedUserId;
    private List<EmployeeRoleAssignment>? _cachedEmployeeAssignments;
    private int? _cachedEmployeeId;

    public PermissionService(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<UserKind> ResolveAsync(ClaimsPrincipal user)
    {
        if (_cachedKind.HasValue && _cachedUserId == _users.GetUserId(user))
            return _cachedKind.Value;

        _cachedUserId = _users.GetUserId(user);
        if (string.IsNullOrEmpty(_cachedUserId)) return (_cachedKind = UserKind.None).Value;

        if (user.IsInRole(IdentityRoles.SuperAdmin) || user.IsInRole(IdentityRoles.Admin))
            return (_cachedKind = UserKind.SuperAdmin).Value;

        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == _cachedUserId);
        if (employee != null)
        {
            _cachedEmployeeId = employee.Id;
            return (_cachedKind = UserKind.Employee).Value;
        }

        return (_cachedKind = UserKind.None).Value;
    }

    public async Task<bool> CanAsync(ClaimsPrincipal user, string serviceId, string permission, HrScope? scope = null)
    {
        var kind = await ResolveAsync(user);
        if (kind == UserKind.SuperAdmin) return true;
        if (kind != UserKind.Employee) return false;

        var assignments = await GetEmployeeAssignmentsAsync();
        scope ??= HrScope.Global();

        foreach (var a in assignments)
        {
            if (!a.HrRole!.Permissions.Any(p => p.ServiceId == serviceId && p.Permission == permission))
                continue;

            if (await AssignmentCoversScopeAsync(a, scope))
                return true;
        }

        return false;
    }

    public async Task<IReadOnlySet<string>> AllowedServicesAsync(ClaimsPrincipal user, HrScope scope)
    {
        var kind = await ResolveAsync(user);
        var result = new HashSet<string>();

        if (kind == UserKind.SuperAdmin)
        {
            foreach (var s in ServiceCatalog.Services) result.Add(s.Id);
            return result;
        }

        if (kind != UserKind.Employee) return result;

        var assignments = await GetEmployeeAssignmentsAsync();
        foreach (var a in assignments)
        {
            if (!await AssignmentCoversScopeAsync(a, scope)) continue;
            foreach (var p in a.HrRole!.Permissions)
                if (p.ServiceId != HrSystemPermissions.ServiceId)
                    result.Add(p.ServiceId);
        }
        return result;
    }

    public async Task<IReadOnlyList<Company>> VisibleCompaniesAsync(ClaimsPrincipal user)
    {
        var kind = await ResolveAsync(user);
        List<Company> companies;

        if (kind == UserKind.SuperAdmin)
        {
            companies = await _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).ToListAsync();
        }
        else if (kind != UserKind.Employee || !_cachedEmployeeId.HasValue)
        {
            return Array.Empty<Company>();
        }
        else
        {
            var assignments = await GetEmployeeAssignmentsAsync();
            var companyIds = new HashSet<int>();

            // own company
            var ownCompanyId = await _db.Employees
                .Where(e => e.Id == _cachedEmployeeId.Value)
                .Select(e => (int?)e.CompanyId)
                .FirstOrDefaultAsync();
            if (ownCompanyId.HasValue) companyIds.Add(ownCompanyId.Value);

            // Global-level assignment covers all companies; otherwise add specifics.
            var hasGlobal = assignments.Any(a => a.ScopeLevel == ScopeLevel.Global);
            if (hasGlobal)
            {
                companies = await _db.Companies.AsNoTracking().OrderBy(c => c.NameAr).ToListAsync();
            }
            else
            {
                foreach (var a in assignments)
                {
                    if (a.CompanyId.HasValue) companyIds.Add(a.CompanyId.Value);
                    else if (a.BranchId.HasValue)
                    {
                        var cid = await _db.Branches
                            .Where(br => br.Id == a.BranchId.Value)
                            .Select(br => (int?)br.CompanyId)
                            .FirstOrDefaultAsync();
                        if (cid.HasValue) companyIds.Add(cid.Value);
                    }
                }
                companies = await _db.Companies.AsNoTracking()
                    .Where(c => companyIds.Contains(c.Id))
                    .OrderBy(c => c.NameAr)
                    .ToListAsync();
            }
        }

        // Overwrite stale counters with real DB counts
        var ids = companies.Select(c => c.Id).ToList();
        var bc = await _db.Branches
            .Where(b => ids.Contains(b.CompanyId))
            .GroupBy(b => b.CompanyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var ec = await _db.Employees
            .Where(e => ids.Contains(e.CompanyId))
            .GroupBy(e => e.CompanyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        foreach (var c in companies)
        {
            c.BranchesCount  = bc.GetValueOrDefault(c.Id);
            c.EmployeesCount = ec.GetValueOrDefault(c.Id);
        }

        return companies;
    }

    public async Task<IReadOnlyList<Branch>> VisibleBranchesAsync(ClaimsPrincipal user, int companyId)
    {
        var kind = await ResolveAsync(user);
        List<Branch> branches;

        if (kind == UserKind.SuperAdmin)
        {
            branches = await _db.Branches.AsNoTracking()
                .Where(br => br.CompanyId == companyId)
                .OrderBy(br => br.NameAr)
                .ToListAsync();
        }
        else if (kind != UserKind.Employee || !_cachedEmployeeId.HasValue)
        {
            return Array.Empty<Branch>();
        }
        else
        {
            var assignments = await GetEmployeeAssignmentsAsync();
            var branchIds = new HashSet<int>();

            // own branch
            var ownBranchId = await _db.Employees
                .Where(e => e.Id == _cachedEmployeeId.Value && e.CompanyId == companyId)
                .Select(e => (int?)e.BranchId)
                .FirstOrDefaultAsync();
            if (ownBranchId.HasValue) branchIds.Add(ownBranchId.Value);

            foreach (var a in assignments)
            {
                bool coversAllInCompany = a.ScopeLevel == ScopeLevel.Global
                                          || (a.ScopeLevel == ScopeLevel.Company && a.CompanyId == companyId);

                if (coversAllInCompany)
                {
                    var allInCompany = await _db.Branches
                        .Where(br => br.CompanyId == companyId)
                        .Select(br => br.Id)
                        .ToListAsync();
                    foreach (var id in allInCompany) branchIds.Add(id);
                }
                else if (a.BranchId.HasValue)
                {
                    var match = await _db.Branches
                        .AnyAsync(br => br.Id == a.BranchId.Value && br.CompanyId == companyId);
                    if (match) branchIds.Add(a.BranchId.Value);
                }
            }

            branches = await _db.Branches.AsNoTracking()
                .Where(br => branchIds.Contains(br.Id))
                .OrderBy(br => br.NameAr)
                .ToListAsync();
        }

        // Overwrite stale employee counters with real DB counts
        var bids = branches.Select(b => b.Id).ToList();
        var empCounts = await _db.Employees
            .Where(e => bids.Contains(e.BranchId))
            .GroupBy(e => e.BranchId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        foreach (var b in branches)
            b.EmployeesCount = empCounts.GetValueOrDefault(b.Id);

        return branches;
    }

    // ─── helpers ─────────────────────────────────────────────────────────

    private async Task<List<EmployeeRoleAssignment>> GetEmployeeAssignmentsAsync()
    {
        if (_cachedEmployeeAssignments != null) return _cachedEmployeeAssignments;
        if (!_cachedEmployeeId.HasValue) return _cachedEmployeeAssignments = new();

        _cachedEmployeeAssignments = await _db.EmployeeRoleAssignments
            .AsNoTracking()
            .Where(a => a.EmployeeId == _cachedEmployeeId.Value)
            .Include(a => a.HrRole!).ThenInclude(r => r.Permissions)
            .ToListAsync();
        return _cachedEmployeeAssignments;
    }

    private async Task<bool> AssignmentCoversScopeAsync(EmployeeRoleAssignment a, HrScope scope)
    {
        // Global covers everything.
        // Company-level covers everything under that company.
        // Branch-level covers only that branch.
        // A branch/company-scoped assignment must never satisfy a Global-scope request —
        // CanAsync defaults to Global when no scope is passed, so this guards every
        // permission check across the app from being granted to the wrong company/branch.

        if (a.ScopeLevel == ScopeLevel.Global) return true;

        switch (scope.Level)
        {
            case ScopeLevel.Global:
                return false;

            case ScopeLevel.Company:
                if (!scope.CompanyId.HasValue) return false;
                if (a.ScopeLevel == ScopeLevel.Company)
                    return a.CompanyId == scope.CompanyId;
                if (a.ScopeLevel == ScopeLevel.Branch && a.BranchId.HasValue)
                    return await _db.Branches.AnyAsync(br => br.Id == a.BranchId.Value && br.CompanyId == scope.CompanyId);
                return false;

            case ScopeLevel.Branch:
                if (!scope.BranchId.HasValue) return false;
                if (a.ScopeLevel == ScopeLevel.Company && a.CompanyId.HasValue)
                    return await _db.Branches.AnyAsync(br => br.Id == scope.BranchId && br.CompanyId == a.CompanyId);
                if (a.ScopeLevel == ScopeLevel.Branch)
                    return a.BranchId == scope.BranchId;
                return false;
        }

        return false;
    }
}
