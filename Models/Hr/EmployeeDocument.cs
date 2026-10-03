using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr;

public enum EmployeeDocType
{
    NationalId, Passport, Iqama, DrivingLicense, Cv, Contract, EducationCertificate, IbanCertificate, Other
}

// 1:many with Employee.
public class EmployeeDocument
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    public EmployeeDocType DocType { get; set; }
    [MaxLength(160)] public string? CustomLabel { get; set; }

    [MaxLength(260)] public string FileName { get; set; } = "";
    [MaxLength(120)] public string ContentType { get; set; } = "";
    public byte[] FileData { get; set; } = Array.Empty<byte>();
    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? UploadedByUserId { get; set; }
}
