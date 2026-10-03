using Project_HR_1.Models.Hr;

namespace Project_HR_1.Services.Reports;

// Reverse direction of EmployeeImportColumns: given a column Header key, format that
// field's *current* value on an Employee for display (roster review page / PDF).
// Employee must be loaded with Branch, JobTitle, Department, Sponsor, Personal,
// Financial, Work (+ Work.ActualJobTitle, Work.DirectSupervisor) for full coverage.
public static class EmployeeColumnReader
{
    private static string Dt(DateOnly? d) => d?.ToString("yyyy-MM-dd") ?? "";
    private static string Dec(decimal? d) => d?.ToString("N2") ?? "";
    private static string YesNo(bool b) => b ? "نعم" : "لا";

    // Printable columns that are NOT part of the import template (EmployeeImportColumns) — report-only.
    public static readonly IReadOnlyList<EmployeeImportColumn> ExtraColumns = new[]
    {
        new EmployeeImportColumn("Status", "حالة الموظف", "", false, "", ""),
    };

    // InitialPassword is intentionally excluded — never displayed/printed.
    public static readonly IReadOnlyDictionary<string, Func<Employee, string>> Readers =
        new Dictionary<string, Func<Employee, string>>
        {
            ["EmployeeCode"] = e => e.EmployeeCode,
            ["Status"] = e => EmployeeStatuses.Get(e.Status).NameAr,
            ["Email"] = e => e.Email ?? "",
            ["FirstNameAr"] = e => e.FirstNameAr ?? "",
            ["FatherNameAr"] = e => e.FatherNameAr ?? "",
            ["GrandfatherNameAr"] = e => e.GrandfatherNameAr ?? "",
            ["FamilyNameAr"] = e => e.FamilyNameAr ?? "",
            ["FirstNameEn"] = e => e.FirstNameEn ?? "",
            ["FatherNameEn"] = e => e.FatherNameEn ?? "",
            ["GrandfatherNameEn"] = e => e.GrandfatherNameEn ?? "",
            ["FamilyNameEn"] = e => e.FamilyNameEn ?? "",
            ["BranchSlug"] = e => e.Branch?.NameAr ?? "",

            ["DepartmentName"] = e => e.Department?.NameAr ?? "",
            ["SponsorName"] = e => e.Sponsor?.NameAr ?? "",
            ["Nationality"] = e => e.NationalityAr ?? e.NationalityCode ?? "",
            ["JobTitleName"] = e => e.JobTitle?.NameAr ?? "",
            ["CostCenter"] = e => e.CostCenter ?? "",
            ["HireDate"] = e => Dt(e.HireDate),
            ["LastWorkDate"] = e => Dt(e.LastWorkDate),
            ["BasicSalary"] = e => e.BasicSalary.ToString("N2"),
            ["Phone"] = e => e.Phone ?? "",

            ["NationalId"] = e => e.NationalId ?? "",
            ["NationalIdExpiry"] = e => Dt(e.NationalIdExpiry),
            ["IqamaNumber"] = e => e.IqamaNumber ?? "",
            ["IqamaExpiry"] = e => Dt(e.IqamaExpiry),
            ["PassportNumber"] = e => e.PassportNumber ?? "",
            ["PassportExpiry"] = e => Dt(e.PassportExpiry),
            // Not currently persisted anywhere on Employee/EmployeePersonal.
            ["BirthDate"] = e => "",

            ["MaritalStatus"] = e => e.Personal?.MaritalStatus?.ToString() ?? "",
            ["ChildrenCount"] = e => e.Personal?.ChildrenCount?.ToString() ?? "",
            ["BloodType"] = e => e.Personal?.BloodType ?? "",
            ["City"] = e => e.Personal?.City ?? "",
            ["District"] = e => e.Personal?.District ?? "",
            ["Street"] = e => e.Personal?.Street ?? "",
            ["EmergencyContactName"] = e => e.Personal?.EmergencyContactName ?? "",
            ["EmergencyContactPhone"] = e => e.Personal?.EmergencyContactPhone ?? "",

            ["IsEducated"] = e => e.Personal == null ? "" : YesNo(e.Personal.IsEducated),
            ["EducationLevel"] = e => e.Personal?.EducationLevel ?? "",
            ["EducationMajor"] = e => e.Personal?.EducationMajor ?? "",
            ["EducationInstitution"] = e => e.Personal?.EducationInstitution ?? "",
            ["EducationCountry"] = e => e.Personal?.EducationCountry ?? "",
            ["GraduationYear"] = e => e.Personal?.GraduationYear?.ToString() ?? "",
            ["Gpa"] = e => e.Personal?.Gpa?.ToString("N2") ?? "",
            ["CertificateNumber"] = e => e.Personal?.CertificateNumber ?? "",

            ["BankName"] = e => e.Financial?.BankName ?? "",
            ["Iban"] = e => e.Financial?.Iban ?? "",
            ["BeneficiaryName"] = e => e.Financial?.BeneficiaryName ?? "",
            ["HousingAllowance"] = e => Dec(e.Financial?.HousingAllowance),
            ["TransportAllowance"] = e => Dec(e.Financial?.TransportAllowance),
            ["OtherAllowance"] = e => Dec(e.Financial?.OtherAllowance),
            ["OtherAllowanceDescription"] = e => e.Financial?.OtherAllowanceDescription ?? "",
            ["EndOfServiceAdvance"] = e => Dec(e.Financial?.EndOfServiceAdvance),
            ["SalaryType"] = e => e.Financial?.SalaryType.ToString() ?? "",
            ["DailyRate"] = e => Dec(e.Financial?.DailyRate),
            ["ContractTotalValue"] = e => Dec(e.Financial?.ContractTotalValue),
            ["FinancialContractDurationMonths"] = e => e.Financial?.ContractDurationMonths?.ToString() ?? "",

            ["ActualJobTitleName"] = e => e.Work?.ActualJobTitle?.NameAr ?? "",
            ["DirectSupervisorCode"] = e => e.Work?.DirectSupervisor?.NameAr ?? "",
            ["WorkLocation"] = e => e.Work?.WorkLocation ?? "",
            ["WorkSystem"] = e => e.Work?.WorkSystem.ToString() ?? "",
            ["DailyWorkHours"] = e => e.Work?.DailyWorkHours?.ToString("N2") ?? "",
            ["ContractType"] = e => e.Work?.ContractType.ToString() ?? "",
            ["ContractStartDate"] = e => Dt(e.ContractStartDate),
            ["ContractEndDate"] = e => Dt(e.ContractEndDate),
            ["ContractDurationMonths"] = e => e.Work?.ContractDurationMonths?.ToString() ?? "",
            ["ProbationDays"] = e => e.Work?.ProbationDays.ToString() ?? "",
            ["AnnualLeaveBalance"] = e => e.Work?.AnnualLeaveBalance.ToString() ?? "",
            ["LastLeaveDate"] = e => Dt(e.Work?.LastLeaveDate),
            ["SickLeaveUsedDays"] = e => e.Work?.SickLeaveUsedDays.ToString() ?? "",
        };
}
