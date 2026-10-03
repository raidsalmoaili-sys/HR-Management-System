namespace Project_HR_1.Services.Audit;

public interface IActivityLogger
{
    Task LogAsync(
        string entityType,
        string entityId,
        string action,
        string? userId = null,
        int? employeeId = null,
        string? details = null,
        int? companyId = null,
        int? branchId = null,
        string? ipAddress = null);
}
