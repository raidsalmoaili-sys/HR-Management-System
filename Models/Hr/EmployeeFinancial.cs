using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_HR_1.Models.Hr;

public enum SalaryType { Monthly, Daily, Contract }

// 1:1 with Employee — EmployeeId is both PK and FK.
public class EmployeeFinancial
{
    public int EmployeeId { get; set; }
    [ForeignKey(nameof(EmployeeId))] public Employee? Employee { get; set; }

    // Bank
    [MaxLength(120)] public string? BankName { get; set; }
    [MaxLength(34)]  public string? Iban { get; set; }
    [MaxLength(200)] public string? BeneficiaryName { get; set; }

    // Allowances
    public decimal HousingAllowance { get; set; }
    public decimal TransportAllowance { get; set; }
    public decimal OtherAllowance { get; set; }
    [MaxLength(300)] public string? OtherAllowanceDescription { get; set; }

    // Server-enforced: only non-Saudi employees may have a value here (see EmployeesController).
    public decimal? EndOfServiceAdvance { get; set; }

    public SalaryType SalaryType { get; set; } = SalaryType.Monthly;
    public decimal? DailyRate { get; set; }
    public decimal? ContractTotalValue { get; set; }
    public int? ContractDurationMonths { get; set; }
}
