using System.Security.Claims;
using Project_HR_1.Models.Hr;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Services.Permissions;

public interface IPermissionService
{
    Task<UserKind> ResolveAsync(ClaimsPrincipal user);

    Task<bool> CanAsync(ClaimsPrincipal user, string serviceId, string permission, HrScope? scope = null);

    Task<IReadOnlySet<string>> AllowedServicesAsync(ClaimsPrincipal user, HrScope scope);

    Task<IReadOnlyList<Company>> VisibleCompaniesAsync(ClaimsPrincipal user);
    Task<IReadOnlyList<Branch>>  VisibleBranchesAsync(ClaimsPrincipal user, int companyId);
}
