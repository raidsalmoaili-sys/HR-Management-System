using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr;

public enum MaritalStatus { Single, Married, Divorced, Widowed }

// 1:1 with Employee — EmployeeId is both PK and FK.
public class EmployeePersonal
{
    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }
    public int? ChildrenCount { get; set; }
    [MaxLength(8)] public string? BloodType { get; set; }

    [MaxLength(120)] public string? City { get; set; }
    [MaxLength(120)] public string? District { get; set; }
    [MaxLength(200)] public string? Street { get; set; }

    [MaxLength(200)] public string? EmergencyContactName { get; set; }
    [MaxLength(20)]  public string? EmergencyContactPhone { get; set; }

    // ── Education (only meaningful when IsEducated = true; nulled out otherwise) ──
    public bool IsEducated { get; set; }
    [MaxLength(40)]  public string? EducationLevel { get; set; }
    [MaxLength(160)] public string? EducationMajor { get; set; }
    [MaxLength(200)] public string? EducationInstitution { get; set; }
    [MaxLength(100)] public string? EducationCountry { get; set; }
    public int? GraduationYear { get; set; }
    public decimal? Gpa { get; set; }
    [MaxLength(60)]  public string? CertificateNumber { get; set; }
}
