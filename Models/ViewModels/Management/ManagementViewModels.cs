using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Project_HR_1.Models.Hr;
using Project_HR_1.Models.Hr.Audit;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Support;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.ViewModels.Management;

// ─── Companies ────────────────────────────────────────────────────────

public class CompaniesListVM
{
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class CompanyEditVM
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string NameAr { get; set; } = "";
    [Required, MaxLength(200)] public string NameEn { get; set; } = "";
    [MaxLength(20)]  public string? UnifiedNumber { get; set; }
    [MaxLength(20)]  public string? EstablishmentNumber { get; set; }
    [MaxLength(20)]  public string? CommercialRegistration { get; set; }
    [MaxLength(120)] public string? SectorAr { get; set; }
    [MaxLength(8)]   public string? EstablishedYear { get; set; }
    [MaxLength(300)] public string? HeadquartersAr { get; set; }
}

// ─── Branches ─────────────────────────────────────────────────────────

public class BranchesListVM
{
    public IReadOnlyList<Branch> Branches { get; set; } = Array.Empty<Branch>();
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public int? CompanyFilter { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class BranchEditVM
{
    public int Id { get; set; }
    [Required] public int CompanyId { get; set; }
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();

    [Required, MaxLength(200)] public string NameAr { get; set; } = "";
    [MaxLength(200)]           public string? NameEn { get; set; }
    public bool IsPrimary { get; set; }

    // رخصة الفرع
    [MaxLength(50)] public string?  LicenseNumber    { get; set; }
    public DateOnly?                LicenseStartDate { get; set; }
    public DateOnly?                LicenseEndDate   { get; set; }

    // شهادة ضريبة القيمة المضافة
    [MaxLength(50)] public string?  VatNumber    { get; set; }
    public DateOnly?                VatStartDate { get; set; }
    public DateOnly?                VatEndDate   { get; set; }

    // العنوان الوطني
    [MaxLength(200)] public string?  NatAddressShort  { get; set; }
    public DateOnly?                 NatAddressRegDate { get; set; }
    [MaxLength(20)]  public string?  BuildingNumber   { get; set; }
    [MaxLength(200)] public string?  StreetName       { get; set; }
    [MaxLength(20)]  public string?  SecondaryNumber  { get; set; }
    [MaxLength(120)] public string?  DistrictName     { get; set; }
    [MaxLength(20)]  public string?  PostalCode       { get; set; }
    [MaxLength(120)] public string?  City             { get; set; }
}

// ─── Job Titles ───────────────────────────────────────────────────────

public class JobTitlesListVM
{
    public IReadOnlyList<JobTitle> JobTitles { get; set; } = Array.Empty<JobTitle>();
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public int? CompanyFilter { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class JobTitleEditVM
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    [Required, MaxLength(64)]  public string Code { get; set; } = "";
    [Required, MaxLength(160)] public string NameAr { get; set; } = "";
    [Required, MaxLength(160)] public string NameEn { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// ─── Departments ──────────────────────────────────────────────────────

public class DepartmentsListVM
{
    public IReadOnlyList<Department> Departments { get; set; } = Array.Empty<Department>();
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public int? CompanyFilter { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class DepartmentEditVM
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    [Required, MaxLength(160)] public string NameAr { get; set; } = "";
    [Required, MaxLength(160)] public string NameEn { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// ─── Sponsors ─────────────────────────────────────────────────────────

public class SponsorsListVM
{
    public IReadOnlyList<Sponsor> Sponsors { get; set; } = Array.Empty<Sponsor>();
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public int? CompanyFilter { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class SponsorEditVM
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    [Required, MaxLength(200)] public string NameAr { get; set; } = "";
    [Required, MaxLength(200)] public string NameEn { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// ─── Tickets ──────────────────────────────────────────────────────────

public class TicketsListVM
{
    public IReadOnlyList<SupportTicket> Tickets { get; set; } = Array.Empty<SupportTicket>();
    public TicketStatus? StatusFilter { get; set; }
    public bool CanManage { get; set; }
}

public class TicketDetailVM
{
    public SupportTicket Ticket { get; set; } = null!;
    public bool CanManage { get; set; }
}

// ─── Login + Activity logs ────────────────────────────────────────────

public class LoginLogListVM
{
    public IReadOnlyList<LoginLog> Entries { get; set; } = Array.Empty<LoginLog>();
}

public class ActivityLogListVM
{
    public IReadOnlyList<ActivityLog> Entries { get; set; } = Array.Empty<ActivityLog>();
}

// ─── HR Roles ─────────────────────────────────────────────────────────

public class HrRolesListVM
{
    public IReadOnlyList<HrRole> Roles { get; set; } = Array.Empty<HrRole>();
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class HrRoleCellVM
{
    public string ServiceId { get; set; } = "";
    public string Permission { get; set; } = "";
    public bool Checked { get; set; }
}

public class HrRoleEditVM
{
    public int Id { get; set; }
    [Required, MaxLength(64)]  public string Code { get; set; } = "";
    [Required, MaxLength(160)] public string NameAr { get; set; } = "";
    [Required, MaxLength(160)] public string NameEn { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public List<HrRoleCellVM> Cells { get; set; } = new();

    public static HrRoleEditVM Empty()
    {
        var vm = new HrRoleEditVM();
        foreach (var s in ServiceCatalog.Services)
        {
            // Business permissions per real service.
            foreach (var p in HrBusinessPermissions.All)
                vm.Cells.Add(new HrRoleCellVM { ServiceId = s.Id, Permission = p });
        }
        // System (sentinel) permissions.
        foreach (var p in HrSystemPermissions.All)
            vm.Cells.Add(new HrRoleCellVM { ServiceId = HrSystemPermissions.ServiceId, Permission = p });
        return vm;
    }
}

// ─── Role Assignments ─────────────────────────────────────────────────

public class RoleAssignmentsListVM
{
    public IReadOnlyList<EmployeeRoleAssignment> Assignments { get; set; } = Array.Empty<EmployeeRoleAssignment>();
    public IReadOnlyList<Employee> Employees { get; set; } = Array.Empty<Employee>();
    public IReadOnlyList<HrRole> Roles { get; set; } = Array.Empty<HrRole>();
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public IReadOnlyList<Branch> Branches { get; set; } = Array.Empty<Branch>();
    public bool CanCreate { get; set; }
    public bool CanDelete { get; set; }
}

public class CreateAssignmentVM
{
    [Required] public int EmployeeId { get; set; }
    [Required] public int HrRoleId { get; set; }
    [Required] public ScopeLevel ScopeLevel { get; set; }
    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }
}

// ─── Employees ────────────────────────────────────────────────────────

// Query-string filters for Employees/Index (GET ?q=&status=&companyId=&branchId=&nationality=).
public class EmployeeListFilter
{
    public string? Q { get; set; }
    public string? Status { get; set; }
    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }
    public string? Nationality { get; set; }   // "SA" | "NON_SA"

    public bool Any => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrEmpty(Status) || CompanyId.HasValue
                       || BranchId.HasValue || !string.IsNullOrEmpty(Nationality);
}

public class EmployeesListVM
{
    public IReadOnlyList<Employee> Employees { get; set; } = Array.Empty<Employee>();
    public EmployeeListFilter Filter { get; set; } = new();
    public int TotalCount { get; set; }
    public IReadOnlyDictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>();
    public IReadOnlyList<Company> FilterCompanies { get; set; } = Array.Empty<Company>();
    public IReadOnlyList<Branch> FilterBranches { get; set; } = Array.Empty<Branch>();
    public bool CanCreate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanEditStatus { get; set; }
    public AddEmployeeVM AddEmployee { get; set; } = new();
}

public class EmployeeDetailVM
{
    public Employee Employee { get; set; } = null!;
    public IReadOnlyList<EmployeeRoleAssignment> Assignments { get; set; } = Array.Empty<EmployeeRoleAssignment>();
    public bool CanTransfer { get; set; }
    public TransferEmployeeVM Transfer { get; set; } = new();
    public bool CanEdit { get; set; }
    /// <summary>Email of the linked login account (null = employee has no login).</summary>
    public string? LoginEmail { get; set; }
    public bool CanAssignRoles { get; set; }
}

// Quick manual fix of the fields the sheet import often leaves wrong/empty (ID, nationality, IBAN...).
// Posted from the "تعديل البيانات الأساسية" modal on Employees/Detail. Branch changes use Transfer.
public class EditEmployeeBasicsVM
{
    public string? NationalityCode { get; set; }
    [MaxLength(20)] public string? NationalId { get; set; }
    public DateOnly? NationalIdExpiry { get; set; }
    [MaxLength(20)] public string? IqamaNumber { get; set; }
    public DateOnly? IqamaExpiry { get; set; }
    [MaxLength(20)] public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(256)] public string? Email { get; set; }
    [MaxLength(160)] public string? JobTitleAr { get; set; }
    public decimal? BasicSalary { get; set; }
    [MaxLength(120)] public string? BankName { get; set; }
    [MaxLength(40)] public string? Iban { get; set; }
}

public class TransferEmployeeVM
{
    public int BranchId { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }

    // ── Dropdown sources (not posted) ───────────────────────────────────
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public IReadOnlyList<Branch>  Branches  { get; set; } = Array.Empty<Branch>();
}

public class AddEmployeeVM
{
    // ── البيانات العامة ──────────────────────────────────────────────────
    // Project has <Nullable>enable</Nullable>, so every non-nullable `string` here would be
    // treated by MVC as implicitly [Required] (with an English message). Optional fields are
    // therefore declared `string?`, and the truly required ones carry an explicit Arabic [Required].
    // See docs/features/employee-full-profile-form.md (2026-09-27 change).

    // Login account is OPTIONAL: leave both blank to add the employee as a record only (no login).
    // If either one is filled, both are required (enforced in EmployeesController.Create).
    [MaxLength(256)]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    public string? Email { get; set; }
    [DataType(DataType.Password)] public string? InitialPassword { get; set; }

    [Required(ErrorMessage = "الاسم الأول مطلوب.")]
    [MaxLength(60)] public string FirstNameAr { get; set; } = "";
    [MaxLength(60)] public string? FatherNameAr { get; set; }
    [MaxLength(60)] public string? GrandfatherNameAr { get; set; }
    [Required(ErrorMessage = "اسم العائلة مطلوب.")]
    [MaxLength(60)] public string FamilyNameAr { get; set; } = "";
    [MaxLength(60)] public string? FirstNameEn { get; set; }
    [MaxLength(60)] public string? FatherNameEn { get; set; }
    [MaxLength(60)] public string? GrandfatherNameEn { get; set; }
    [MaxLength(60)] public string? FamilyNameEn { get; set; }

    [Required(ErrorMessage = "الرقم الوظيفي مطلوب.")]
    [MaxLength(32)] public string EmployeeCode { get; set; } = "";
    // CompanyId is derived from the selected branch on the server; not posted by the form.
    public int CompanyId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "الفرع (جهة العمل) مطلوب.")]
    public int BranchId { get; set; }
    public int? JobTitleId { get; set; }
    // Optional — a company may not have any departments/sponsors defined yet.
    public int? DepartmentId { get; set; }
    public int? SponsorId { get; set; }
    public string NationalityCode { get; set; } = "SA";
    [MaxLength(120)] public string? CostCenter { get; set; }

    public DateOnly? HireDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? LastWorkDate { get; set; }
    public decimal BasicSalary { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }

    // ── البيانات الشخصية ─────────────────────────────────────────────────
    [MaxLength(20)] public string? NationalId { get; set; }
    public DateOnly? NationalIdExpiry { get; set; }
    [MaxLength(20)] public string? IqamaNumber { get; set; }
    public DateOnly? IqamaExpiry { get; set; }
    [MaxLength(20)] public string? PassportNumber { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public DateOnly? BirthDate { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }
    public int? ChildrenCount { get; set; }
    [MaxLength(8)] public string? BloodType { get; set; }
    [MaxLength(120)] public string? City { get; set; }
    [MaxLength(120)] public string? District { get; set; }
    [MaxLength(200)] public string? Street { get; set; }
    [MaxLength(200)] public string? EmergencyContactName { get; set; }
    [MaxLength(20)]  public string? EmergencyContactPhone { get; set; }

    public bool IsEducated { get; set; }
    [MaxLength(40)]  public string? EducationLevel { get; set; }
    [MaxLength(160)] public string? EducationMajor { get; set; }
    [MaxLength(200)] public string? EducationInstitution { get; set; }
    [MaxLength(100)] public string? EducationCountry { get; set; }
    public int? GraduationYear { get; set; }
    public decimal? Gpa { get; set; }
    [MaxLength(60)] public string? CertificateNumber { get; set; }

    // ── البيانات المالية ─────────────────────────────────────────────────
    [MaxLength(120)] public string? BankName { get; set; }
    public string? Iban { get; set; }
    [MaxLength(200)] public string? BeneficiaryName { get; set; }

    public decimal HousingAllowance { get; set; }
    public decimal TransportAllowance { get; set; }
    public decimal OtherAllowance { get; set; }
    [MaxLength(300)] public string? OtherAllowanceDescription { get; set; }
    public decimal? EndOfServiceAdvance { get; set; }

    public SalaryType SalaryType { get; set; } = SalaryType.Monthly;
    public decimal? DailyRate { get; set; }
    public decimal? ContractTotalValue { get; set; }
    public int? FinancialContractDurationMonths { get; set; }

    // ── م. العمل والإجازة ────────────────────────────────────────────────
    public int? ActualJobTitleId { get; set; }
    public int? DirectSupervisorId { get; set; }
    [MaxLength(200)] public string? WorkLocation { get; set; }
    public WorkSystem WorkSystem { get; set; } = WorkSystem.FullTime;
    public decimal? DailyWorkHours { get; set; }

    public ContractType ContractType { get; set; } = ContractType.Unlimited;
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public int? ContractDurationMonths { get; set; }
    public int ProbationDays { get; set; } = 90;

    public int AnnualLeaveBalance { get; set; }
    public DateOnly? LastLeaveDate { get; set; }
    public int SickLeaveUsedDays { get; set; }

    // ── الوثائق الرسمية ──────────────────────────────────────────────────
    public IFormFile? NationalIdFile { get; set; }
    public IFormFile? PassportFile { get; set; }
    public IFormFile? IqamaFile { get; set; }
    public IFormFile? DrivingLicenseFile { get; set; }
    public IFormFile? CvFile { get; set; }
    public IFormFile? ContractFile { get; set; }
    public IFormFile? EducationCertificateFile { get; set; }
    public IFormFile? IbanCertificateFile { get; set; }
    public List<string> CustomDocumentLabels { get; set; } = new();
    public List<IFormFile> CustomDocumentFiles { get; set; } = new();

    // ── Footer actions (not displayed as data) ──────────────────────────
    public bool SaveAndAddAnother { get; set; }

    // ── Dropdown sources (not posted) ───────────────────────────────────
    public IReadOnlyList<Company>    Companies    { get; set; } = Array.Empty<Company>();
    public IReadOnlyList<Branch>     Branches     { get; set; } = Array.Empty<Branch>();
    public IReadOnlyList<JobTitle>   JobTitles    { get; set; } = Array.Empty<JobTitle>();
    public IReadOnlyList<Department> Departments  { get; set; } = Array.Empty<Department>();
    public IReadOnlyList<Sponsor>    Sponsors     { get; set; } = Array.Empty<Sponsor>();
    public IReadOnlyList<Employee>   Supervisors  { get; set; } = Array.Empty<Employee>();
    public IReadOnlyList<LookupItem> Banks        { get; set; } = Lookups.SaudiBanks;
    public IReadOnlyList<LookupItem> Nationalities { get; set; } = Lookups.Nationalities;
}

// ─── Employee Bulk Import ─────────────────────────────────────────────

public class BulkImportVM
{
    public IReadOnlyList<Branch> Branches { get; set; } = Array.Empty<Branch>();
    public IReadOnlyList<EmployeeImportColumn> Columns { get; set; } = EmployeeImportColumns.All;
}

public class BulkImportResultVM
{
    public int Imported { get; set; }
    public int TotalRows { get; set; }
    public List<BulkImportErrorRow> Errors { get; set; } = new();
}

public class BulkImportErrorRow
{
    public int    RowNumber    { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string Email        { get; set; } = "";
    public string Error        { get; set; } = "";
}

// ─── Employee Roster Print ────────────────────────────────────────────

public class PrintRosterSetupVM
{
    public IReadOnlyList<Company> Companies { get; set; } = Array.Empty<Company>();
    public IReadOnlyList<Branch> Branches { get; set; } = Array.Empty<Branch>();
    // InitialPassword excluded — never shown/printed.
    public IReadOnlyList<EmployeeImportColumn> Columns { get; set; } =
        EmployeeImportColumns.All.Where(c => c.Header != "InitialPassword").ToList();

    // Filter lookups (all already narrowed to the active HR scope).
    public IReadOnlyList<Department> Departments { get; set; } = Array.Empty<Department>();
    public IReadOnlyList<JobTitle> JobTitles { get; set; } = Array.Empty<JobTitle>();
    public IReadOnlyList<Sponsor> Sponsors { get; set; } = Array.Empty<Sponsor>();
    public IReadOnlyList<LookupItem> Nationalities { get; set; } = Array.Empty<LookupItem>();

    // Live employee counts (within the active scope) shown next to each company/branch.
    public IReadOnlyDictionary<int, int> CompanyEmployeeCounts { get; set; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> BranchEmployeeCounts { get; set; } = new Dictionary<int, int>();
    public int TotalEmployees { get; set; }
}

public class PrintRosterRequestVM
{
    // ── Scope ──
    public int[]? CompanyIds { get; set; }
    public int[]? BranchIds { get; set; }

    // ── Columns (in the order they should be printed) ──
    public List<string> Columns { get; set; } = new();

    // ── Filters (all optional; empty = no condition) ──
    public string? Q { get; set; }                 // name / code / national id / iqama / phone
    public string? Status { get; set; }            // "active" | "leave" | "inactive"
    public string? Nationality { get; set; }       // "SA" | "NON_SA" | a Lookups.Nationalities code
    public int? DepartmentId { get; set; }
    public int? JobTitleId { get; set; }
    public int? SponsorId { get; set; }
    public DateOnly? HiredFrom { get; set; }
    public DateOnly? HiredTo { get; set; }
    public string? ContractType { get; set; }      // "Limited" | "Unlimited"
    public int? DocExpiryDays { get; set; }        // 0 = already expired, else expires within N days (ID or Iqama)

    // ── Output options ──
    public string Orientation { get; set; } = "landscape";   // "landscape" | "portrait"
    public bool RowNumbers { get; set; } = true;
    public bool ShowFilters { get; set; } = true;
    public bool AutoPrint { get; set; }                       // preview page opens the print dialog on load
}

public class PrintRosterResultVM
{
    public List<string> ColumnHeaders { get; set; } = new();
    public List<string> ColumnKeys { get; set; } = new();
    public List<string[]> Rows { get; set; } = new();
    public int EmployeeCount { get; set; }
    public string ScopeSummary { get; set; } = "";
    public List<string> FilterSummary { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public string GeneratedBy { get; set; } = "";

    // The exact request, carried forward as hidden inputs so PDF/Excel regenerate the same selection.
    public PrintRosterRequestVM Request { get; set; } = new();

    // Kept for backwards compatibility with older views/links.
    public int[] CompanyIds { get; set; } = Array.Empty<int>();
    public int[] BranchIds { get; set; } = Array.Empty<int>();
    public List<string> Columns { get; set; } = new();
}
