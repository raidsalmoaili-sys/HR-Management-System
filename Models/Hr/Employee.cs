using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.Hr;

public class Employee : IScoped
{
    public int Id { get; set; }

    [MaxLength(32)]
    public string EmployeeCode { get; set; } = "";

    [MaxLength(450)]
    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [MaxLength(200)] public string NameAr { get; set; } = "";
    [MaxLength(200)] public string NameEn { get; set; } = "";
    [MaxLength(8)]   public string Initials { get; set; } = "";

    // ── Granular name parts (optional; NameAr/NameEn above are auto-derived from these when set) ──
    [MaxLength(60)] public string? FirstNameAr { get; set; }
    [MaxLength(60)] public string? FatherNameAr { get; set; }
    [MaxLength(60)] public string? GrandfatherNameAr { get; set; }
    [MaxLength(60)] public string? FamilyNameAr { get; set; }
    [MaxLength(60)] public string? FirstNameEn { get; set; }
    [MaxLength(60)] public string? FatherNameEn { get; set; }
    [MaxLength(60)] public string? GrandfatherNameEn { get; set; }
    [MaxLength(60)] public string? FamilyNameEn { get; set; }

    [MaxLength(160)] public string? JobTitleAr { get; set; }
    [MaxLength(160)] public string? JobTitleEn { get; set; }
    [MaxLength(160)] public string? DeptAr { get; set; }

    public int CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))] public Company? Company { get; set; }

    public int BranchId { get; set; }
    [ForeignKey(nameof(BranchId))] public Branch? Branch { get; set; }

    public int? JobTitleId { get; set; }
    [ForeignKey(nameof(JobTitleId))] public JobTitle? JobTitle { get; set; }

    public int? DepartmentId { get; set; }
    [ForeignKey(nameof(DepartmentId))] public Department? Department { get; set; }

    public int? SponsorId { get; set; }
    [ForeignKey(nameof(SponsorId))] public Sponsor? Sponsor { get; set; }

    [MaxLength(120)] public string? CostCenter { get; set; }
    public decimal BasicSalary { get; set; }
    [MaxLength(20)]  public string? Phone { get; set; }
    public DateOnly? LastWorkDate { get; set; }

    [MaxLength(32)]  public string Status { get; set; } = "active";
    [MaxLength(16)]  public string? AvatarColor { get; set; }
    [MaxLength(256)] public string? Email { get; set; }

    // ── Nationality & documents ──────────────────────────────────────────
    [MaxLength(4)]   public string? NationalityCode { get; set; }  // "SA", "EG", "IN", etc.
    [MaxLength(100)] public string? NationalityAr   { get; set; }
    [MaxLength(100)] public string? NationalityEn   { get; set; }

    // Saudi national ID (مواطنون)
    [MaxLength(20)] public string? NationalId       { get; set; }
    public DateOnly? NationalIdExpiry               { get; set; }

    // Iqama (مقيمون)
    [MaxLength(20)] public string? IqamaNumber      { get; set; }
    public DateOnly? IqamaExpiry                    { get; set; }

    // Passport (الكل)
    [MaxLength(20)] public string? PassportNumber   { get; set; }
    public DateOnly? PassportExpiry                 { get; set; }

    // Contract
    public DateOnly? ContractStartDate              { get; set; }
    public DateOnly? ContractEndDate                { get; set; }

    // Hire date
    public DateOnly? HireDate                       { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Sub-tables (1:1 / 1:many profile data) ────────────────────────────
    public EmployeePersonal? Personal { get; set; }
    public EmployeeFinancial? Financial { get; set; }
    public EmployeeWork? Work { get; set; }
    public List<EmployeeDocument> Documents { get; set; } = new();

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => BranchId;
}
