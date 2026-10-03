using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr.HrPermissions;

public static class HrSystemPermissions
{
    public const string ServiceId = "_system";
    public const string ManageHrRoles      = "manage-hr-roles";
    public const string AssignHrRoles      = "assign-hr-roles";
    public const string ApproveEmployees   = "approve-employees";
    public const string ViewEmployees      = "view-employees";

    public static readonly string[] All =
    {
        ManageHrRoles, AssignHrRoles, ApproveEmployees, ViewEmployees,
    };
}

public static class HrBusinessPermissions
{
    public const string View    = "view";
    public const string Create  = "create";
    public const string Edit    = "edit";
    public const string Delete  = "delete";
    public const string Approve = "approve";

    public static readonly string[] All = { View, Create, Edit, Delete, Approve };
}

public class HrRolePermission
{
    public int Id { get; set; }

    public int HrRoleId { get; set; }
    [ForeignKey(nameof(HrRoleId))]
    public HrRole? HrRole { get; set; }

    [MaxLength(64)]
    public string ServiceId { get; set; } = "";

    [MaxLength(32)]
    public string Permission { get; set; } = "";
}
