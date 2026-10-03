using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.Hr;

public class Company : IScoped
{
    public int Id { get; set; }

    [MaxLength(64)]  public string Slug { get; set; } = "";
    [MaxLength(8)]   public string Initials { get; set; } = "";

    [MaxLength(200)] public string NameAr { get; set; } = "";
    [MaxLength(200)] public string NameEn { get; set; } = "";
    [MaxLength(200)] public string? ShortNameAr { get; set; }
    [MaxLength(200)] public string? ShortNameEn { get; set; }

    [MaxLength(20)]  public string? UnifiedNumber { get; set; }
    [MaxLength(20)]  public string? EstablishmentNumber { get; set; }
    [MaxLength(20)]  public string? CommercialRegistration { get; set; }

    [MaxLength(120)] public string? SectorAr { get; set; }
    [MaxLength(120)] public string? SectorEn { get; set; }

    [MaxLength(300)] public string? HeadquartersAr { get; set; }
    [MaxLength(300)] public string? HeadquartersEn { get; set; }

    public int EmployeesCount { get; set; }
    public int BranchesCount { get; set; }

    [MaxLength(8)]   public string? EstablishedYear { get; set; }

    public List<Branch> Branches { get; set; } = new();

    // Note: a Company has no BranchId of its own, so IScopeContext.Filter<T>() (which
    // filters by BranchId at Branch-level scope) isn't a good fit here — controllers
    // filter Companies manually by CompanyId, which is populated at both Company and
    // Branch scope levels.
    int? IScoped.CompanyId => Id;
    int? IScoped.BranchId => null;
}
