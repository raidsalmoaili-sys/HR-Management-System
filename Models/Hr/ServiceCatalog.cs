namespace Project_HR_1.Models.Hr;

public static class ServiceCatalog
{
    public const string SystemCategoryId = "system";

    public static readonly IReadOnlyList<HrServiceCategory> Categories = new[]
    {
        new HrServiceCategory("core",         "أساسي",          "Core"),
        new HrServiceCategory("employees",    "الموظفون",       "Employees"),
        new HrServiceCategory("compensation", "التعويضات",      "Compensation"),
        new HrServiceCategory("time",         "الوقت والإجازات", "Time & Leave"),
        new HrServiceCategory("performance",  "الأداء",         "Performance"),
        new HrServiceCategory("compliance",   "الامتثال",       "Compliance"),
        new HrServiceCategory("lifecycle",    "دورة الخدمة",    "Service Lifecycle"),
        new HrServiceCategory(SystemCategoryId, "النظام",       "System"),
    };

    public static readonly IReadOnlyList<HrService> Services = new[]
    {
        // Core
        new HrService("dashboard",      "لوحة التحكم",        "Dashboard",       "fa-chart-pie",       "#1a2744", "core"),
        new HrService("org-structure",  "الهيكل التنظيمي",     "Org Structure",   "fa-sitemap",         "#8e44ad", "core"),
        new HrService("alerts",         "التنبيهات",          "Alerts",          "fa-bell",            "#e74c3c", "core", 12),
        new HrService("tasks",          "المهام",             "Tasks",           "fa-list-check",      "#2980b9", "core",  5),

        // Employees
        new HrService("employees",      "قاعدة بيانات الموظفين", "Employee Database", "fa-id-card",      "#1a2744", "employees"),
        new HrService("attendance",     "التحضير",            "Attendance",      "fa-fingerprint",     "#16a085", "employees"),

        // Compensation
        new HrService("payroll",        "مسيرات الرواتب",      "Payroll",         "fa-money-bill-wave", "#27ae60", "compensation"),
        new HrService("allowances",     "البدلات",            "Allowances",      "fa-hand-holding-dollar", "#c8a84b", "compensation"),
        new HrService("deductions",     "الحسميات",           "Deductions",      "fa-circle-minus",    "#e74c3c", "compensation"),
        new HrService("bonuses",        "المكافآت",           "Bonuses",         "fa-award",           "#f39c12", "compensation"),
        new HrService("raises",         "العلاوات",           "Raises",          "fa-arrow-trend-up",  "#27ae60", "compensation"),
        new HrService("advances",       "السلفة",             "Salary Advance",  "fa-piggy-bank",      "#3498db", "compensation"),
        new HrService("expenses",       "التكاليف",           "Expenses",        "fa-receipt",         "#9b59b6", "compensation"),

        // Time & Leave
        new HrService("leaves",         "الإجازات",           "Leaves",          "fa-umbrella-beach",  "#3498db", "time"),
        new HrService("leave-tickets",  "الإجازات والتذاكر",   "Leaves & Tickets","fa-plane-departure", "#5dade2", "time"),
        new HrService("delegations",    "الانتدابات",         "Delegations",     "fa-route",           "#34495e", "time"),

        // Performance
        new HrService("evaluations",    "التقييم",            "Evaluation",      "fa-chart-line",      "#16a085", "performance"),
        new HrService("courses",        "الدورات",            "Training",        "fa-graduation-cap",  "#9b59b6", "performance"),

        // Compliance
        new HrService("contracts",      "العقود",             "Contracts",       "fa-file-contract",   "#1a2744", "compliance"),
        new HrService("documents",      "الوثائق الرسمية",     "Official Documents","fa-folder-open",   "#7f8c9a", "compliance"),
        new HrService("violations",     "المخالفات",          "Violations",      "fa-gavel",           "#e67e22", "compliance"),
        new HrService("warnings",       "الإنذارات",          "Warnings",        "fa-triangle-exclamation", "#e74c3c", "compliance"),

        // Service Lifecycle
        new HrService("custody",        "العهد",              "Custody",         "fa-boxes-stacked",   "#34495e", "lifecycle"),
        new HrService("transfers",      "النقل",              "Transfers",       "fa-arrows-rotate",   "#16a085", "lifecycle"),
        new HrService("resignations",   "الاستقالات",         "Resignations",    "fa-door-open",       "#f39c12", "lifecycle"),
        new HrService("end-of-service", "إنهاء الخدمة",       "End of Service",  "fa-power-off",       "#c0392b", "lifecycle"),

        // System (formerly IT Portal + Owner Portal settings)
        new HrService("manage-companies",          "إدارة الشركات",          "Manage Companies",  "fa-building",       "#1a2744", SystemCategoryId),
        new HrService("manage-branches",           "إدارة الفروع",           "Manage Branches",   "fa-code-branch",    "#16a085", SystemCategoryId),
        new HrService("manage-jobs",               "المسميات الوظيفية",      "Job Titles",        "fa-id-badge",       "#8e44ad", SystemCategoryId),
        new HrService("manage-departments",        "الإدارات",              "Departments",       "fa-sitemap",        "#16a085", SystemCategoryId),
        new HrService("manage-sponsors",           "الكفلاء",               "Sponsors",          "fa-user-tie",       "#c0392b", SystemCategoryId),
        new HrService("manage-tickets",            "تذاكر الدعم",           "Support Tickets",   "fa-headset",        "#e67e22", SystemCategoryId),
        new HrService("view-login-log",            "سجل الدخول",            "Login Log",         "fa-right-to-bracket","#7f8c9a", SystemCategoryId),
        new HrService("view-activity-log",         "سجل النشاط",            "Activity Log",      "fa-clock-rotate-left","#34495e", SystemCategoryId),
        new HrService("settings-hr-roles",         "الأدوار",                "Roles",             "fa-user-shield",    "#1a2744", SystemCategoryId),
        new HrService("settings-role-assignments", "الصلاحيات",              "Permissions",       "fa-people-arrows",  "#8e44ad", SystemCategoryId),
    };

    public static readonly IReadOnlyList<HrService> BusinessServices =
        Services.Where(s => s.Category != SystemCategoryId).ToList();

    public static readonly IReadOnlyList<string> PinnedQuickJump = new[]
    {
        "dashboard", "org-structure", "employees", "attendance",
        "leaves", "payroll", "contracts", "tasks", "alerts",
    };

    public static HrService? ById(string id) => Services.FirstOrDefault(s => s.Id == id);
    public static IEnumerable<HrService> ByCategory(string catId) => Services.Where(s => s.Category == catId);
    public static HrServiceCategory? CategoryById(string id) => Categories.FirstOrDefault(c => c.Id == id);
}
