using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.Hr;

public class Branch : IScoped
{
    public int Id { get; set; }

    [MaxLength(64)]  public string Slug { get; set; } = "";

    public int CompanyId { get; set; }
    [ForeignKey(nameof(CompanyId))] public Company? Company { get; set; }

    [MaxLength(200)] public string NameAr { get; set; } = "";
    [MaxLength(200)] public string? NameEn { get; set; }

    public bool IsPrimary { get; set; }
    public int EmployeesCount { get; set; }

    // رخصة الفرع
    [MaxLength(50)]  public string? LicenseNumber { get; set; }
    public DateOnly? LicenseStartDate { get; set; }
    public DateOnly? LicenseEndDate   { get; set; }

    // شهادة ضريبة القيمة المضافة
    [MaxLength(50)]  public string? VatNumber { get; set; }
    public DateOnly? VatStartDate { get; set; }
    public DateOnly? VatEndDate   { get; set; }

    // العنوان الوطني
    [MaxLength(200)] public string? NatAddressShort  { get; set; }
    public DateOnly? NatAddressRegDate { get; set; }
    [MaxLength(20)]  public string? BuildingNumber   { get; set; }
    [MaxLength(200)] public string? StreetName       { get; set; }
    [MaxLength(20)]  public string? SecondaryNumber  { get; set; }
    [MaxLength(120)] public string? DistrictName     { get; set; }
    [MaxLength(20)]  public string? PostalCode       { get; set; }
    [MaxLength(120)] public string? City             { get; set; }

    public List<Employee> Employees { get; set; } = new();

    int? IScoped.CompanyId => CompanyId;
    int? IScoped.BranchId => Id;
}
