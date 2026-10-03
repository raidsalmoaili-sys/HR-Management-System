using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.Hr;

public class JobTitle : IScoped
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Code { get; set; } = "";

    [MaxLength(160)] public string NameAr { get; set; } = "";
    [MaxLength(160)] public string NameEn { get; set; } = "";

    public int CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))] public Company? Company { get; set; }

    public bool IsActive { get; set; } = true;

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => null;
}
