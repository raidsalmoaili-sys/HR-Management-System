namespace Project_HR_1.Models.Hr;

public record EmployeeImportColumn(
    string Header,   // English name (used for legacy-format detection)
    string NameAr,   // Arabic label shown in the professional template
    string Sample,
    bool Required,
    string NoteAr,
    string NoteEn
);

// Sections define the visual grouping in the Excel template.
// FromCol/ToCol are 1-based column indices into All.
public record ImportSection(string NameAr, string NameEn, int FromCol, int ToCol, int R, int G, int B);

public static class EmployeeImportColumns
{
    public static readonly IReadOnlyList<ImportSection> Sections = new[]
    {
        new ImportSection("البيانات الأساسية",   "Basic Info",            1,  12, 26,  39,  68),
        new ImportSection("التصنيف الوظيفي",     "Job Classification",   13,  21, 13,  80,  55),
        new ImportSection("وثائق الهوية",        "Identity Documents",   22,  28, 80,  30, 160),
        new ImportSection("البيانات الشخصية",    "Personal Details",     29,  36, 180, 83,   9),
        new ImportSection("التعليم",             "Education",            37,  44,  3, 105, 161),
        new ImportSection("البيانات المالية",    "Financial Data",       45,  56, 153, 27,  27),
        new ImportSection("عقد العمل",           "Employment Contract",  57,  69, 55,  65,  81),
    };

    // Light pastel background per section (for optional columns).
    public static readonly IReadOnlyList<(int R, int G, int B)> SectionLightColors = new[]
    {
        (220, 228, 245),   // Basic   — light navy
        (210, 240, 225),   // Job     — light green
        (230, 218, 248),   // Docs    — light purple
        (252, 235, 210),   // Personal— light amber
        (212, 237, 252),   // Edu     — light blue
        (253, 220, 220),   // Finance — light red
        (228, 232, 240),   // Contract— light gray
    };

    public static readonly IReadOnlyList<EmployeeImportColumn> All = new[]
    {
        // ── البيانات الأساسية (1-12) ──────────────────────────────
        new EmployeeImportColumn("EmployeeCode",        "رمز الموظف",             "EMP-001",                true,  "رمز فريد للموظف",                                       "Unique employee code"),
        new EmployeeImportColumn("Email",               "البريد الإلكتروني",      "ahmed@example.com",      true,  "بريد فريد، يُستخدم كاسم مستخدم للدخول",                "Unique email used as login"),
        new EmployeeImportColumn("InitialPassword",     "كلمة المرور",            "Demo@1234",              true,  "6 أحرف على الأقل",                                       "Minimum 6 characters"),
        new EmployeeImportColumn("FirstNameAr",         "الاسم الأول (ع)",        "أحمد",                   true,  "الاسم الأول بالعربي",                                   "First name Arabic"),
        new EmployeeImportColumn("FatherNameAr",        "اسم الأب (ع)",           "محمد",                   true,  "اسم الأب بالعربي",                                      "Father name Arabic"),
        new EmployeeImportColumn("GrandfatherNameAr",   "اسم الجد (ع)",           "علي",                    false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("FamilyNameAr",        "اسم العائلة (ع)",        "العتيبي",                true,  "اسم العائلة بالعربي",                                   "Family name Arabic"),
        new EmployeeImportColumn("FirstNameEn",         "الاسم الأول (إنج)",      "Ahmed",                  false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("FatherNameEn",        "اسم الأب (إنج)",         "Mohammed",               false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("GrandfatherNameEn",   "اسم الجد (إنج)",         "Ali",                    false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("FamilyNameEn",        "اسم العائلة (إنج)",      "Alotaibi",               false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("BranchSlug",          "معرف الفرع",             "branch-slug-here",       true,  "انظر ورقة المرجع للقيم المتاحة",                        "See Reference sheet"),

        // ── التصنيف الوظيفي (13-21) ──────────────────────────────
        new EmployeeImportColumn("DepartmentName",      "اسم الإدارة",            "الموارد البشرية",        false, "اسم الإدارة (عربي) كما هو مسجل في النظام",             "Department name as registered"),
        new EmployeeImportColumn("SponsorName",         "الكفيل",                 "الكفيل التجاري",         false, "اسم الكفيل (عربي) كما هو مسجل في النظام",              "Sponsor name as registered"),
        new EmployeeImportColumn("Nationality",         "الجنسية",                "سعودي",                  false, "اكتب الجنسية كاملة كما في ورقة المرجع — اختياري، تُترك فارغة إن لم تُحدَّد", "Type the full nationality name as in the Reference sheet — optional, left blank if not specified"),
        new EmployeeImportColumn("JobTitleName",        "المسمى الوظيفي",        "أخصائي موارد بشرية",     false, "اكتب المسمى الوظيفي الرسمي بالضبط كما في ورقة المرجع (ينتمي لشركة الفرع)", "Type the official job title exactly as in the Reference sheet — belongs to the branch's company"),
        new EmployeeImportColumn("CostCenter",          "مركز التكلفة",           "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("HireDate",            "تاريخ الالتحاق",         "2026-01-01",             false, "صيغة YYYY-MM-DD",                                       "Format YYYY-MM-DD"),
        new EmployeeImportColumn("LastWorkDate",        "تاريخ آخر يوم",          "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("BasicSalary",         "الراتب الأساسي",         "5000",                   false, "راتب أساسي بالريال",                                    "Basic salary in SAR"),
        new EmployeeImportColumn("Phone",               "رقم الجوال",             "0501234567",             false, "رقم الجوال",                                            "Mobile number"),

        // ── وثائق الهوية (22-28) ─────────────────────────────────
        new EmployeeImportColumn("NationalId",          "رقم الهوية",             "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("NationalIdExpiry",    "انتهاء الهوية",          "",                       false, "صيغة YYYY-MM-DD",                                       "Format YYYY-MM-DD"),
        new EmployeeImportColumn("IqamaNumber",         "رقم الإقامة",            "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("IqamaExpiry",         "انتهاء الإقامة",         "",                       false, "صيغة YYYY-MM-DD",                                       "Format YYYY-MM-DD"),
        new EmployeeImportColumn("PassportNumber",      "رقم الجواز",             "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("PassportExpiry",      "انتهاء الجواز",          "",                       false, "صيغة YYYY-MM-DD",                                       "Format YYYY-MM-DD"),
        new EmployeeImportColumn("BirthDate",           "تاريخ الميلاد",          "",                       false, "صيغة YYYY-MM-DD",                                       "Format YYYY-MM-DD"),

        // ── البيانات الشخصية (29-36) ─────────────────────────────
        new EmployeeImportColumn("MaritalStatus",       "الحالة الاجتماعية",      "Single",                 false, "Single / Married / Divorced / Widowed",                 "Single / Married / Divorced / Widowed"),
        new EmployeeImportColumn("ChildrenCount",       "عدد الأبناء",            "0",                      false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("BloodType",           "فصيلة الدم",             "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("City",                "المدينة",                "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("District",            "الحي",                   "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("Street",              "الشارع",                 "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("EmergencyContactName","جهة الطوارئ",            "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("EmergencyContactPhone","هاتف الطوارئ",          "",                       false, "اختياري",                                               "Optional"),

        // ── التعليم (37-44) ──────────────────────────────────────
        new EmployeeImportColumn("IsEducated",          "يحمل مؤهل؟",            "false",                  false, "true أو false — تُهمل حقول التعليم إن كانت false",     "true/false — education fields ignored when false"),
        new EmployeeImportColumn("EducationLevel",      "المستوى التعليمي",       "",                       false, "ابتدائي/متوسط/ثانوي/دبلوم/بكالوريوس/ماجستير/دكتوراه", "Education level"),
        new EmployeeImportColumn("EducationMajor",      "التخصص",                 "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("EducationInstitution","المؤسسة التعليمية",      "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("EducationCountry",    "دولة التعليم",           "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("GraduationYear",      "سنة التخرج",             "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("Gpa",                 "المعدل التراكمي",        "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("CertificateNumber",   "رقم الشهادة",            "",                       false, "اختياري",                                               "Optional"),

        // ── البيانات المالية (45-56) ─────────────────────────────
        new EmployeeImportColumn("BankName",            "اسم البنك",              "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("Iban",                "رقم الآيبان (IBAN)",     "",                       false, "SA + 22 رقماً",                                         "SA + 22 digits"),
        new EmployeeImportColumn("BeneficiaryName",     "اسم المستفيد",           "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("HousingAllowance",    "بدل السكن",              "0",                      false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("TransportAllowance",  "بدل النقل",              "0",                      false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("OtherAllowance",      "بدل آخر",                "0",                      false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("OtherAllowanceDescription","وصف البدل",         "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("EndOfServiceAdvance", "نهاية الخدمة (مقدم)",   "",                       false, "يُهمل تلقائياً إن كانت الجنسية سعودية",                "Ignored automatically for Saudi nationals"),
        new EmployeeImportColumn("SalaryType",          "نوع الراتب",             "Monthly",                false, "Monthly / Daily / Contract",                            "Monthly / Daily / Contract"),
        new EmployeeImportColumn("DailyRate",           "الأجر اليومي",           "",                       false, "تُستخدم إذا كان نوع الراتب Daily",                      "Used when SalaryType is Daily"),
        new EmployeeImportColumn("ContractTotalValue",  "إجمالي قيمة العقد",     "",                       false, "تُستخدم إذا كان نوع الراتب Contract",                   "Used when SalaryType is Contract"),
        new EmployeeImportColumn("FinancialContractDurationMonths","مدة العقد (مالي)","",                   false, "تُستخدم إذا كان نوع الراتب Contract",                   "Used when SalaryType is Contract"),

        // ── عقد العمل (57-69) ────────────────────────────────────
        new EmployeeImportColumn("ActualJobTitleName",  "المسمى الوظيفي الفعلي", "",                       false, "اكتب المسمى الوظيفي بالضبط كما في ورقة المرجع (ينتمي لشركة الفرع)", "Type the job title exactly as in the Reference sheet — belongs to the branch's company"),
        new EmployeeImportColumn("DirectSupervisorCode","رمز المشرف المباشر",    "",                       false, "رمز موظف موجود مسبقاً في النظام",                       "Existing employee code"),
        new EmployeeImportColumn("WorkLocation",        "موقع العمل",             "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("WorkSystem",          "نظام العمل",             "FullTime",               false, "FullTime / PartTime / Remote",                          "FullTime / PartTime / Remote"),
        new EmployeeImportColumn("DailyWorkHours",      "ساعات العمل اليومية",   "8",                      false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("ContractType",        "نوع العقد",              "Unlimited",              false, "Limited / Unlimited",                                   "Limited / Unlimited"),
        new EmployeeImportColumn("ContractStartDate",   "تاريخ بدء العقد",        "",                       false, "صيغة YYYY-MM-DD",                                       "Format YYYY-MM-DD"),
        new EmployeeImportColumn("ContractEndDate",     "تاريخ انتهاء العقد",     "",                       false, "صيغة YYYY-MM-DD",                                       "Format YYYY-MM-DD"),
        new EmployeeImportColumn("ContractDurationMonths","مدة العقد (شهور)",     "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("ProbationDays",       "فترة التجربة (يوم)",    "90",                     false, "افتراضي 90",                                            "Defaults to 90"),
        new EmployeeImportColumn("AnnualLeaveBalance",  "رصيد الإجازة السنوية",  "0",                      false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("LastLeaveDate",       "تاريخ آخر إجازة",        "",                       false, "اختياري",                                               "Optional"),
        new EmployeeImportColumn("SickLeaveUsedDays",   "أيام الإجازة المرضية",  "0",                      false, "اختياري",                                               "Optional"),
    };
}
