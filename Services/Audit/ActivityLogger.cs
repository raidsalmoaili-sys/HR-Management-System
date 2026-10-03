using Microsoft.AspNetCore.Http;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr.Audit;

namespace Project_HR_1.Services.Audit;

public class ActivityLogger : IActivityLogger
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public ActivityLogger(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(
        string entityType,
        string entityId,
        string action,
        string? userId = null,
        int? employeeId = null,
        string? details = null,
        int? companyId = null,
        int? branchId = null,
        string? ipAddress = null)
    {
        var ctx = _http.HttpContext;
        userId    ??= ctx?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        ipAddress ??= ctx?.Connection.RemoteIpAddress?.ToString();

        _db.ActivityLogs.Add(new ActivityLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            EmployeeId = employeeId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Details = details,
            CompanyId = companyId,
            BranchId = branchId,
            IpAddress = ipAddress,
        });
        await _db.SaveChangesAsync();
    }
}
