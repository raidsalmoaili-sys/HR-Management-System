using System.ComponentModel.DataAnnotations;

namespace Project_HR_1.Models.Hr.HrPermissions;

public class HrRole
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Code { get; set; } = "";

    [MaxLength(160)] public string NameAr { get; set; } = "";
    [MaxLength(160)] public string NameEn { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public List<HrRolePermission> Permissions { get; set; } = new();
}
