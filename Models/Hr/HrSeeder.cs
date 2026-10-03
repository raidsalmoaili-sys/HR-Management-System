using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_HR_1.Models.Hr.HrPermissions;
using Project_HR_1.Models.Hr.Modules;
using Project_HR_1.Services.HrModules;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;

namespace Project_HR_1.Models.Hr;

/// <summary>
/// Demo data for the portfolio edition — `dotnet run -- seed`.
/// Everything here is FICTIONAL: a made-up group ("مجموعة الأفق القابضة"), random names, random ID/IBAN numbers.
/// The seed is destructive (wipes all HR tables) and deterministic (Random(42)), relative to today's date
/// so the dashboard, alerts and attendance always look "live". See docs/features/hr-core-modules.md
/// </summary>
public static class HrSeeder
{
    public const string SuperAdminEmail    = "admin@local";
    public const string SuperAdminPassword = "Admin123";
    public const string HrManagerEmail     = "hr@demo.local";
    public const string BranchViewerEmail  = "viewer@demo.local";
    public const string DemoPassword       = "Demo1234";

    private static readonly Random Rng = new(42);

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db          = sp.GetRequiredService<AppDbContext>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureIdentityRolesAsync(roleManager);
        var admin = await EnsureSuperAdminAsync(userManager);

        await WipeHrTablesAsync(db, userManager);
        var roles = await SeedHrRolesAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var org = await SeedOrganizationAsync(db);
        var employees = await SeedEmployeesAsync(db, org, today);
        await SeedDemoUsersAsync(db, userManager, roles, admin.Id, employees);

        await SeedContractsAsync(db, employees, today);
        await SeedLeavesAsync(db, employees, admin.Id, today);
        await SeedAttendanceAsync(db, employees, today);
        await SeedAdvancesAsync(db, employees, admin.Id, today);
        await SeedPayrollAsync(db, org.Companies, admin.Id, today);
        await UpdateCountersAsync(db);

        Console.WriteLine($"[seed] {org.Companies.Count} companies, {org.Branches.Count} branches, {employees.Count} employees.");
        Console.WriteLine($"[seed] SuperAdmin   : {SuperAdminEmail} / {SuperAdminPassword}");
        Console.WriteLine($"[seed] HR manager   : {HrManagerEmail} / {DemoPassword}   (hr-manager, all companies)");
        Console.WriteLine($"[seed] Branch viewer: {BranchViewerEmail} / {DemoPassword}   (view only, one branch)");
    }

    // ═══ Identity ═══════════════════════════════════════════════════════════

    private static async Task EnsureIdentityRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync(IdentityRoles.SuperAdmin))
            await roleManager.CreateAsync(new IdentityRole(IdentityRoles.SuperAdmin));
        if (!await roleManager.RoleExistsAsync(IdentityRoles.Admin))
            await roleManager.CreateAsync(new IdentityRole(IdentityRoles.Admin));
    }

    private static async Task<ApplicationUser> EnsureSuperAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(SuperAdminEmail);
        if (user == null)
        {
            user = new ApplicationUser { UserName = SuperAdminEmail, Email = SuperAdminEmail, EmailConfirmed = true };
            var r = await userManager.CreateAsync(user, SuperAdminPassword);
            if (!r.Succeeded)
                throw new Exception("Failed to create SuperAdmin: " + string.Join("; ", r.Errors.Select(e => e.Description)));
        }
        if (!await userManager.IsInRoleAsync(user, IdentityRoles.SuperAdmin))
            await userManager.AddToRoleAsync(user, IdentityRoles.SuperAdmin);
        return user;
    }

    private static async Task WipeHrTablesAsync(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        // Children before parents (several FKs are Restrict).
        await db.PayrollItems.ExecuteDeleteAsync();
        await db.PayrollRuns.ExecuteDeleteAsync();
        await db.AttendanceRecords.ExecuteDeleteAsync();
        await db.LeaveRequests.ExecuteDeleteAsync();
        await db.SalaryAdvances.ExecuteDeleteAsync();
        await db.EmploymentContracts.ExecuteDeleteAsync();

        await db.EmployeeRoleAssignments.ExecuteDeleteAsync();
        await db.HrRolePermissions.ExecuteDeleteAsync();
        await db.HrRoles.ExecuteDeleteAsync();

        await db.ActivityLogs.ExecuteDeleteAsync();
        await db.LoginLogs.ExecuteDeleteAsync();
        await db.SupportTickets.ExecuteDeleteAsync();

        await db.EmployeeDocuments.ExecuteDeleteAsync();
        await db.EmployeeWorks.ExecuteDeleteAsync();
        await db.EmployeeFinancials.ExecuteDeleteAsync();
        await db.EmployeePersonals.ExecuteDeleteAsync();
        await db.Employees.ExecuteDeleteAsync();

        await db.JobTitles.ExecuteDeleteAsync();
        await db.Departments.ExecuteDeleteAsync();
        await db.Sponsors.ExecuteDeleteAsync();
        await db.Branches.ExecuteDeleteAsync();
        await db.Companies.ExecuteDeleteAsync();

        // Delete every non-admin account (demo users are recreated below).
        var users = await db.Users.Where(u => u.Email != SuperAdminEmail).ToListAsync();
        foreach (var u in users)
        {
            var r = await userManager.GetRolesAsync(u);
            if (!r.Contains(IdentityRoles.SuperAdmin) && !r.Contains(IdentityRoles.Admin))
                await userManager.DeleteAsync(u);
        }
    }

    private record SeededRoles(HrRole FullAccess, HrRole Manager, HrRole ViewOnly);

    private static async Task<SeededRoles> SeedHrRolesAsync(AppDbContext db)
    {
        // hr-full-access — every business service × every permission + every _system permission
        var fullAccess = new HrRole { Code = "hr-full-access", NameAr = "صلاحية كاملة", NameEn = "Full Access", IsSystem = true };
        foreach (var svc in ServiceCatalog.BusinessServices)
            foreach (var perm in HrBusinessPermissions.All)
                fullAccess.Permissions.Add(new HrRolePermission { ServiceId = svc.Id, Permission = perm });
        foreach (var p in HrSystemPermissions.All)
            fullAccess.Permissions.Add(new HrRolePermission { ServiceId = HrSystemPermissions.ServiceId, Permission = p });

        // hr-manager — the day-to-day HR services
        var manager = new HrRole { Code = "hr-manager", NameAr = "مدير موارد بشرية", NameEn = "HR Manager", IsSystem = true };
        var managerServices = new[] { "dashboard", "employees", "attendance", "leaves", "payroll", "contracts", "advances" };
        foreach (var sid in managerServices)
            foreach (var perm in new[] { HrBusinessPermissions.View, HrBusinessPermissions.Create, HrBusinessPermissions.Edit, HrBusinessPermissions.Approve })
                manager.Permissions.Add(new HrRolePermission { ServiceId = sid, Permission = perm });
        manager.Permissions.Add(new HrRolePermission { ServiceId = HrSystemPermissions.ServiceId, Permission = HrSystemPermissions.ViewEmployees });

        // hr-view-only — every business service × view only
        var viewOnly = new HrRole { Code = "hr-view-only", NameAr = "اطلاع فقط", NameEn = "View Only", IsSystem = true };
        foreach (var svc in ServiceCatalog.BusinessServices)
            viewOnly.Permissions.Add(new HrRolePermission { ServiceId = svc.Id, Permission = HrBusinessPermissions.View });
        viewOnly.Permissions.Add(new HrRolePermission { ServiceId = HrSystemPermissions.ServiceId, Permission = HrSystemPermissions.ViewEmployees });

        db.HrRoles.AddRange(fullAccess, manager, viewOnly);
        await db.SaveChangesAsync();
        return new SeededRoles(fullAccess, manager, viewOnly);
    }

    // ═══ Organization ═══════════════════════════════════════════════════════

    private record TitleSpec(string Code, string Ar, string En, string Dept, int MinSalary, int MaxSalary, double SaudiChance, int Count, bool HqOnly = false);

    private record CompanySpec(string Slug, string Initials, string Prefix, string NameAr, string NameEn, string ShortAr, string ShortEn,
        string SectorAr, string SectorEn, string Year, (string Ar, string En, string City)[] Branches,
        (string Ar, string En)[] Departments, TitleSpec[] Titles);

    private static readonly CompanySpec[] Group =
    {
        new("ofoq-trading", "AT", "AT", "شركة الأفق للتجارة", "Al-Ofoq Trading Co.", "الأفق للتجارة", "Ofoq Trading",
            "تجارة التجزئة والجملة", "Retail & wholesale", "2009",
            new[] { ("الإدارة العامة — الرياض", "Head Office — Riyadh", "الرياض"), ("فرع جدة", "Jeddah Branch", "جدة"), ("فرع الدمام", "Dammam Branch", "الدمام") },
            new[] { ("الإدارة العامة", "General Management"), ("الموارد البشرية", "Human Resources"), ("المالية", "Finance"),
                    ("المبيعات", "Sales"), ("المستودعات", "Warehouses"), ("تقنية المعلومات", "IT") },
            new TitleSpec[]
            {
                new("GM",  "المدير العام",          "General Manager",     "الإدارة العامة",   30000, 30000, 1.0, 1, true),
                new("HRM", "مدير الموارد البشرية",   "HR Manager",          "الموارد البشرية",  17000, 17000, 1.0, 1, true),
                new("HRS", "أخصائي موارد بشرية",     "HR Specialist",       "الموارد البشرية",   8000, 10000, 1.0, 2),
                new("FM",  "المدير المالي",          "Finance Manager",     "المالية",          18000, 18000, 0.5, 1, true),
                new("ACC", "محاسب",                  "Accountant",          "المالية",           7000,  9500, 0.4, 3),
                new("SS",  "مشرف مبيعات",            "Sales Supervisor",    "المبيعات",          8000, 11000, 0.7, 3),
                new("SR",  "مندوب مبيعات",           "Sales Representative","المبيعات",          4500,  7000, 0.5, 7),
                new("CSH", "كاشير",                  "Cashier",             "المبيعات",          4000,  5000, 0.9, 4),
                new("WK",  "أمين مستودع",            "Storekeeper",         "المستودعات",        4000,  6000, 0.2, 3),
                new("DRV", "سائق توصيل",             "Delivery Driver",     "المستودعات",        2500,  3500, 0.0, 3),
                new("IT",  "أخصائي تقنية معلومات",    "IT Specialist",       "تقنية المعلومات",   9000, 13000, 0.6, 2),
            }),
        new("ofoq-medical", "AM", "AM", "شركة الأفق للخدمات الطبية", "Al-Ofoq Medical Services", "الأفق الطبية", "Ofoq Medical",
            "الرعاية الصحية", "Healthcare", "2014",
            new[] { ("مجمع الأفق الطبي — الرياض", "Ofoq Medical Complex — Riyadh", "الرياض"), ("عيادات الخبر", "Khobar Clinics", "الخبر") },
            new[] { ("الإدارة الطبية", "Medical Administration"), ("الأطباء", "Physicians"), ("التمريض", "Nursing"),
                    ("المختبر", "Laboratory"), ("الاستقبال", "Reception") },
            new TitleSpec[]
            {
                new("MD",  "المدير الطبي",           "Medical Director",    "الإدارة الطبية",   32000, 32000, 1.0, 1, true),
                new("ADM", "مسؤول شؤون الموظفين",     "Personnel Officer",   "الإدارة الطبية",    9000,  9000, 1.0, 1, true),
                new("SP",  "طبيب أخصائي",            "Specialist Physician","الأطباء",          22000, 27000, 0.3, 3),
                new("GP",  "طبيب عام",               "General Practitioner","الأطباء",          14000, 18000, 0.3, 3),
                new("NR",  "ممرض/ممرضة",             "Nurse",               "التمريض",           6000,  9000, 0.2, 7),
                new("LAB", "فني مختبر",              "Lab Technician",      "المختبر",           6500,  8500, 0.5, 3),
                new("REC", "موظف استقبال",            "Receptionist",        "الاستقبال",         4500,  6000, 1.0, 4),
            }),
        new("waha-contracting", "WC", "WC", "مؤسسة الواحة للمقاولات", "Al-Waha Contracting Est.", "الواحة للمقاولات", "Waha Contracting",
            "المقاولات العامة", "General contracting", "2011",
            new[] { ("المكتب الرئيسي — الرياض", "Main Office — Riyadh", "الرياض"), ("موقع مشروع القصيم", "Qassim Project Site", "بريدة") },
            new[] { ("الإدارة", "Administration"), ("الهندسة", "Engineering"), ("المشاريع", "Projects"),
                    ("المشتريات", "Procurement"), ("السلامة", "HSE") },
            new TitleSpec[]
            {
                new("PM",  "مدير المشاريع",          "Projects Manager",    "الإدارة",          24000, 24000, 1.0, 1, true),
                new("CE",  "مهندس مدني",             "Civil Engineer",      "الهندسة",          11000, 15000, 0.4, 3),
                new("SE",  "مهندس موقع",             "Site Engineer",       "المشاريع",          9000, 12000, 0.3, 2),
                new("SO",  "مسؤول سلامة",            "HSE Officer",         "السلامة",           7000,  9000, 0.6, 1),
                new("PR",  "مسؤول مشتريات",          "Procurement Officer", "المشتريات",         7000,  9000, 0.7, 1),
                new("FMN", "مراقب عمال",             "Foreman",             "المشاريع",          4500,  6000, 0.0, 2),
                new("ELC", "كهربائي",                "Electrician",         "المشاريع",          2800,  3800, 0.0, 3),
                new("LB",  "عامل",                   "Laborer",             "المشاريع",          1800,  2500, 0.0, 6),
                new("DRV", "سائق معدات",             "Equipment Driver",    "المشاريع",          3000,  4000, 0.0, 1),
            }),
    };

    private class OrgData
    {
        public List<Company> Companies { get; } = new();
        public List<Branch> Branches { get; } = new();
        public Dictionary<(int CompanyId, string Dept), Department> Departments { get; } = new();
        public Dictionary<(int CompanyId, string Code), JobTitle> Titles { get; } = new();
        public Dictionary<int, Sponsor> Sponsors { get; } = new();
    }

    private static async Task<OrgData> SeedOrganizationAsync(AppDbContext db)
    {
        var org = new OrgData();
        var n = 0;
        foreach (var spec in Group)
        {
            n++;
            var c = new Company
            {
                Slug = spec.Slug, Initials = spec.Initials, NameAr = spec.NameAr, NameEn = spec.NameEn,
                ShortNameAr = spec.ShortAr, ShortNameEn = spec.ShortEn,
                UnifiedNumber = $"700{n}{Digits(6)}", EstablishmentNumber = $"{n}-{Digits(6)}", CommercialRegistration = $"1010{Digits(6)}",
                SectorAr = spec.SectorAr, SectorEn = spec.SectorEn, EstablishedYear = spec.Year,
                HeadquartersAr = spec.Branches[0].City, HeadquartersEn = spec.Branches[0].En,
            };
            db.Companies.Add(c);
            await db.SaveChangesAsync();
            org.Companies.Add(c);

            var bi = 0;
            foreach (var (ar, en, city) in spec.Branches)
            {
                var b = new Branch
                {
                    CompanyId = c.Id, Slug = $"{spec.Slug}-b{++bi}", NameAr = ar, NameEn = en, IsPrimary = bi == 1, City = city,
                    LicenseNumber = $"{Digits(8)}", LicenseStartDate = DateOnly.FromDateTime(DateTime.Today).AddYears(-2),
                    LicenseEndDate = DateOnly.FromDateTime(DateTime.Today).AddDays(Rng.Next(40, 500)),
                    VatNumber = $"3{Digits(13)}3", BuildingNumber = Digits(4), PostalCode = $"1{Digits(4)}",
                    DistrictName = new[] { "العليا", "الملقا", "الروضة", "الشاطئ", "الفيصلية", "النخيل" }[Rng.Next(6)],
                };
                db.Branches.Add(b);
                org.Branches.Add(b);
            }
            foreach (var (ar, en) in spec.Departments)
            {
                var d = new Department { CompanyId = c.Id, NameAr = ar, NameEn = en };
                db.Departments.Add(d);
                org.Departments[(c.Id, ar)] = d;
            }
            foreach (var t in spec.Titles)
            {
                var jt = new JobTitle { CompanyId = c.Id, Code = $"{spec.Prefix}-{t.Code}", NameAr = t.Ar, NameEn = t.En };
                db.JobTitles.Add(jt);
                org.Titles[(c.Id, t.Code)] = jt;
            }
            var sponsor = new Sponsor { CompanyId = c.Id, NameAr = spec.NameAr, NameEn = spec.NameEn };
            db.Sponsors.Add(sponsor);
            org.Sponsors[c.Id] = sponsor;
            await db.SaveChangesAsync();
        }
        return org;
    }

    // ═══ Employees ══════════════════════════════════════════════════════════

    private static readonly (string Ar, string En)[] SaudiMale =
    {
        ("محمد", "Mohammed"), ("عبدالله", "Abdullah"), ("فهد", "Fahad"), ("خالد", "Khalid"), ("سعود", "Saud"), ("عبدالرحمن", "Abdulrahman"),
        ("تركي", "Turki"), ("فيصل", "Faisal"), ("نايف", "Naif"), ("سلطان", "Sultan"), ("ماجد", "Majed"), ("بندر", "Bandar"),
        ("يوسف", "Yousef"), ("عمر", "Omar"), ("ناصر", "Nasser"), ("راشد", "Rashed"), ("مشاري", "Mishari"), ("زياد", "Ziyad"),
    };
    private static readonly (string Ar, string En)[] SaudiFemale =
    {
        ("نورة", "Noura"), ("سارة", "Sarah"), ("ريم", "Reem"), ("هيفاء", "Haifa"), ("أمل", "Amal"), ("لمى", "Lama"),
        ("جواهر", "Jawaher"), ("منيرة", "Munira"), ("شهد", "Shahad"), ("رزان", "Razan"),
    };
    private static readonly (string Ar, string En)[] SaudiFamilies =
    {
        ("الحربي", "Al-Harbi"), ("العتيبي", "Al-Otaibi"), ("القحطاني", "Al-Qahtani"), ("الشهري", "Al-Shehri"), ("الدوسري", "Al-Dosari"),
        ("الغامدي", "Al-Ghamdi"), ("الزهراني", "Al-Zahrani"), ("المطيري", "Al-Mutairi"), ("السبيعي", "Al-Subaie"), ("العنزي", "Al-Anazi"),
        ("الشمري", "Al-Shammari"), ("البقمي", "Al-Buqami"), ("السهلي", "Al-Sahli"), ("الرشيدي", "Al-Rashidi"),
    };

    // Expat name pools by nationality: (code, first names, family names, weight)
    private static readonly (string Code, (string Ar, string En)[] First, (string Ar, string En)[] Family, int Weight)[] Expats =
    {
        ("EG", new[] { ("أحمد", "Ahmed"), ("محمود", "Mahmoud"), ("مصطفى", "Mostafa"), ("كريم", "Karim"), ("هاني", "Hani"), ("شريف", "Sherif") },
               new[] { ("السيد", "El-Sayed"), ("عبدالعزيز", "Abdelaziz"), ("فوزي", "Fawzy"), ("حسن", "Hassan"), ("رمضان", "Ramadan") }, 5),
        ("IN", new[] { ("راجيش", "Rajesh"), ("سانجاي", "Sanjay"), ("أنيل", "Anil"), ("سوريش", "Suresh"), ("فيجاي", "Vijay") },
               new[] { ("كومار", "Kumar"), ("شارما", "Sharma"), ("ناير", "Nair"), ("بيلاي", "Pillai"), ("ريدي", "Reddy") }, 5),
        ("PK", new[] { ("عمران", "Imran"), ("بلال", "Bilal"), ("عثمان", "Usman"), ("حمزة", "Hamza") },
               new[] { ("أحمد", "Ahmed"), ("حسين", "Hussain"), ("علي", "Ali"), ("رضا", "Raza") }, 4),
        ("PH", new[] { ("ماريا", "Maria"), ("جوزيه", "Jose"), ("آنا", "Ana"), ("مارك", "Mark"), ("جوي", "Joy") },
               new[] { ("سانتوس", "Santos"), ("رييس", "Reyes"), ("كروز", "Cruz"), ("غارسيا", "Garcia") }, 4),
        ("SD", new[] { ("الطيب", "Altayeb"), ("معتز", "Moataz"), ("عوض", "Awad") },
               new[] { ("عثمان", "Osman"), ("بابكر", "Babiker"), ("الأمين", "Alameen") }, 2),
        ("JO", new[] { ("ليث", "Laith"), ("رامي", "Rami"), ("دانة", "Dana") },
               new[] { ("العمري", "Al-Omari"), ("الخطيب", "Al-Khatib"), ("النسور", "Al-Nsour") }, 2),
        ("BD", new[] { ("رحيم", "Rahim"), ("كريم", "Karim"), ("جمال", "Jamal") },
               new[] { ("الدين", "Uddin"), ("الإسلام", "Islam"), ("حسين", "Hossain") }, 3),
        ("YE", new[] { ("عبدالكريم", "Abdulkarim"), ("وليد", "Waleed"), ("صادق", "Sadeq") },
               new[] { ("الحميري", "Al-Humairi"), ("العريقي", "Al-Oraiqi"), ("باوزير", "Bawazir") }, 2),
    };

    private static readonly string[] Colors = { "#1a2744", "#16a085", "#c8a84b", "#8e44ad", "#2563eb", "#dc2626", "#0891b2", "#ea580c" };
    private static readonly string[] Cities = { "الرياض", "جدة", "الدمام", "الخبر", "مكة المكرمة", "المدينة المنورة", "بريدة" };

    private static async Task<List<Employee>> SeedEmployeesAsync(AppDbContext db, OrgData org, DateOnly today)
    {
        var all = new List<Employee>();
        var usedNames = new HashSet<string>();
        for (var ci = 0; ci < Group.Length; ci++)
        {
            var spec = Group[ci];
            var company = org.Companies[ci];
            var branches = org.Branches.Where(b => b.CompanyId == company.Id).ToList();
            var seq = 1000;

            foreach (var t in spec.Titles)
            {
                for (var k = 0; k < t.Count; k++)
                {
                    var saudi = Rng.NextDouble() < t.SaudiChance;
                    var female = saudi ? (t.Code is "NR" or "REC" or "HRS" or "CSH" ? Rng.NextDouble() < 0.6 : Rng.NextDouble() < 0.15) : false;
                    (string Ar, string En) first, father, grand, family;
                    string natCode, natAr, natEn;
                    string nameAr, nameEn;
                    do
                    {
                        if (saudi)
                        {
                            first = female ? Pick(SaudiFemale) : Pick(SaudiMale);
                            father = Pick(SaudiMale); grand = Pick(SaudiMale); family = Pick(SaudiFamilies);
                            natCode = "SA";
                            nameAr = $"{first.Ar} {father.Ar} {family.Ar}";
                            nameEn = $"{first.En} {father.En} {family.En}";
                        }
                        else
                        {
                            var ex = PickWeighted(Expats);
                            first = Pick(ex.First); family = Pick(ex.Family); father = ("", ""); grand = ("", "");
                            natCode = ex.Code;
                            nameAr = $"{first.Ar} {family.Ar}";
                            nameEn = $"{first.En} {family.En}";
                        }
                    } while (!usedNames.Add(nameAr));
                    var nat = Lookups.Nationalities.First(x => x.Code == natCode);
                    natAr = nat.NameAr; natEn = nat.NameEn;

                    var branch = t.HqOnly ? branches[0] : branches[Rng.Next(branches.Count)];
                    var basic = RoundTo(Rng.Next(t.MinSalary, t.MaxSalary + 1), 250);
                    var hire = today.AddDays(-Rng.Next(120, 3000));
                    if (Rng.NextDouble() < 0.12) hire = new DateOnly(today.Year, 1, 1).AddDays(Rng.Next(0, Math.Max(1, today.DayOfYear - 20)));

                    // Contract period: Saudis open-ended, expats 2-year renewable contracts.
                    var contractType = saudi ? ContractType.Unlimited : ContractType.Limited;
                    var cStart = hire;
                    while (contractType == ContractType.Limited && cStart.AddYears(2) < today) cStart = cStart.AddYears(2);
                    DateOnly? cEnd = contractType == ContractType.Limited ? cStart.AddYears(2).AddDays(-1) : null;

                    var e = new Employee
                    {
                        EmployeeCode = $"{spec.Prefix}-{++seq}",
                        NameAr = nameAr, NameEn = nameEn,
                        Initials = Initials(nameAr),
                        FirstNameAr = first.Ar, FirstNameEn = first.En,
                        FatherNameAr = saudi ? father.Ar : null, FatherNameEn = saudi ? father.En : null,
                        GrandfatherNameAr = saudi ? grand.Ar : null, GrandfatherNameEn = saudi ? grand.En : null,
                        FamilyNameAr = family.Ar, FamilyNameEn = family.En,
                        JobTitleAr = t.Ar, JobTitleEn = t.En, DeptAr = t.Dept,
                        CompanyId = company.Id, BranchId = branch.Id,
                        JobTitleId = org.Titles[(company.Id, t.Code)].Id,
                        DepartmentId = org.Departments[(company.Id, t.Dept)].Id,
                        SponsorId = saudi ? null : org.Sponsors[company.Id].Id,
                        BasicSalary = basic,
                        Phone = "05" + Digits(8),
                        Email = $"{Slug(first.En)}.{Slug(family.En)}{Rng.Next(1, 99)}@demo.local",
                        Status = EmployeeStatuses.Active,
                        AvatarColor = Pick(Colors),
                        NationalityCode = natCode, NationalityAr = natAr, NationalityEn = natEn,
                        HireDate = hire,
                        ContractStartDate = cStart, ContractEndDate = cEnd,
                    };
                    if (saudi)
                    {
                        e.NationalId = "1" + Digits(9);
                        e.NationalIdExpiry = today.AddDays(Rng.Next(90, 3000));
                    }
                    else
                    {
                        e.IqamaNumber = "2" + Digits(9);
                        e.IqamaExpiry = today.AddDays(Rng.Next(75, 360));
                        e.PassportNumber = Letters(1) + Digits(8);
                        e.PassportExpiry = today.AddDays(Rng.Next(200, 1800));
                    }

                    var bank = Pick(Lookups.SaudiBanks.Where(b => b.Code != "STCPAY").ToArray());
                    e.Financial = new EmployeeFinancial
                    {
                        BankName = bank.NameAr, Iban = "SA" + Digits(22), BeneficiaryName = nameAr,
                        HousingAllowance = Math.Round(basic * 0.25m), TransportAllowance = Math.Round(basic * 0.10m),
                        OtherAllowance = Rng.NextDouble() < 0.25 ? RoundTo(Rng.Next(200, 800), 50) : 0,
                    };
                    if (e.Financial.OtherAllowance > 0) e.Financial.OtherAllowanceDescription = "بدل اتصال";
                    e.Work = new EmployeeWork
                    {
                        WorkLocation = branch.City, WorkSystem = WorkSystem.FullTime, DailyWorkHours = 8,
                        ContractType = contractType, ContractDurationMonths = contractType == ContractType.Limited ? 24 : null,
                        ProbationDays = 90,
                        AnnualLeaveBalance = Rng.Next(6, 31),
                        LastLeaveDate = today.AddDays(-Rng.Next(60, 400)),
                    };
                    e.Personal = new EmployeePersonal
                    {
                        MaritalStatus = (MaritalStatus)Rng.Next(0, 2),
                        ChildrenCount = Rng.Next(0, 4),
                        City = saudi ? Pick(Cities) : branch.City,
                        District = branch.DistrictName,
                        EmergencyContactName = saudi ? $"{father.Ar} {family.Ar}" : null,
                        EmergencyContactPhone = "05" + Digits(8),
                        IsEducated = true,
                        EducationLevel = basic >= 9000 ? "bachelor" : "secondary",
                    };
                    db.Employees.Add(e);
                    all.Add(e);
                }
            }
        }
        await db.SaveChangesAsync();

        // ── Make the alerts interesting: a few documents expiring soon / already expired ──
        var expats = all.Where(e => e.NationalityCode != "SA").ToList();
        foreach (var e in expats.Take(6)) e.IqamaExpiry = today.AddDays(Rng.Next(5, 55));
        expats[6].IqamaExpiry = today.AddDays(-9);
        expats[7].PassportExpiry = today.AddDays(40);
        foreach (var e in all.Where(e => e.NationalityCode == "SA").Take(2)) e.NationalIdExpiry = today.AddDays(Rng.Next(10, 50));

        // A few expat contracts ending within 60 days (renewal reminders on the dashboard).
        foreach (var e in expats.Skip(10).Take(5))
        {
            e.ContractEndDate = today.AddDays(Rng.Next(12, 58));
            e.ContractStartDate = e.ContractEndDate.Value.AddYears(-2).AddDays(1);
            if (e.HireDate > e.ContractStartDate) e.HireDate = e.ContractStartDate;
        }

        // Two leavers this year (kept for history/payroll).
        foreach (var e in all.Where(e => e.JobTitleAr is "مندوب مبيعات" or "عامل").Skip(1).Take(2))
        {
            e.Status = EmployeeStatuses.Resigned;
            e.LastWorkDate = today.AddDays(-Rng.Next(35, 70));
            if (e.HireDate > e.LastWorkDate) e.HireDate = e.LastWorkDate.Value.AddYears(-2);
        }
        await db.SaveChangesAsync();
        return all;
    }

    private static async Task SeedDemoUsersAsync(AppDbContext db, UserManager<ApplicationUser> userManager, SeededRoles roles,
        string adminId, List<Employee> employees)
    {
        var hrEmp = employees.First(e => e.JobTitleAr == "مدير الموارد البشرية");
        var viewerEmp = employees.Where(e => e.JobTitleAr == "أخصائي موارد بشرية").OrderByDescending(e => e.BranchId).First();

        foreach (var (emp, email, role, level) in new[]
        {
            (hrEmp, HrManagerEmail, roles.Manager, ScopeLevel.Global),
            (viewerEmp, BranchViewerEmail, roles.ViewOnly, ScopeLevel.Branch),
        })
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var r = await userManager.CreateAsync(user, DemoPassword);
            if (!r.Succeeded) throw new Exception($"Failed to create {email}: " + string.Join("; ", r.Errors.Select(x => x.Description)));
            emp.UserId = user.Id;
            emp.Email = email;
            db.EmployeeRoleAssignments.Add(new EmployeeRoleAssignment
            {
                EmployeeId = emp.Id, HrRoleId = role.Id, ScopeLevel = level,
                BranchId = level == ScopeLevel.Branch ? emp.BranchId : null,
                AssignedByUserId = adminId,
            });
        }

        // The general manager of the first company gets full access too (visible in the role-assignment screen).
        var gm = employees.First(e => e.JobTitleAr == "المدير العام");
        db.EmployeeRoleAssignments.Add(new EmployeeRoleAssignment
        {
            EmployeeId = gm.Id, HrRoleId = roles.FullAccess.Id, ScopeLevel = ScopeLevel.Company, CompanyId = gm.CompanyId, AssignedByUserId = adminId,
        });
        await db.SaveChangesAsync();
    }

    // ═══ Contracts ══════════════════════════════════════════════════════════

    private static async Task SeedContractsAsync(AppDbContext db, List<Employee> employees, DateOnly today)
    {
        var counters = new Dictionary<int, int>();
        string Next(int year) => $"CT-{year}-{(counters[year] = counters.GetValueOrDefault(year) + 1):0000}";

        // Leave 3 working employees without a contract so the "without contract" KPI is non-zero.
        var skip = employees.Where(e => e.HireDate > today.AddDays(-150)).Take(3).Select(e => e.Id).ToHashSet();

        foreach (var e in employees.OrderBy(e => e.ContractStartDate))
        {
            if (skip.Contains(e.Id)) continue;
            var f = e.Financial!;
            var start = e.ContractStartDate ?? e.HireDate ?? today;
            var limited = e.Work!.ContractType == ContractType.Limited;

            // Expats renewed at least once get the previous (renewed) contract as history.
            EmploymentContract? previous = null;
            if (limited && e.HireDate < start)
            {
                var pStart = start.AddYears(-2) < e.HireDate!.Value ? e.HireDate.Value : start.AddYears(-2);
                previous = NewContract(e, Next(pStart.Year), pStart, start.AddDays(-1), ContractStatuses.Renewed, Math.Round(e.BasicSalary * 0.92m / 50) * 50);
                db.EmploymentContracts.Add(previous);
                await db.SaveChangesAsync();
            }

            var status = EmployeeStatuses.EndsWork(e.Status) ? ContractStatuses.Terminated : ContractStatuses.Active;
            var c = NewContract(e, Next(start.Year), start, e.ContractEndDate, status, e.BasicSalary);
            c.RenewedFromId = previous?.Id;
            c.ProbationDays = previous == null ? 90 : 0;
            if (status == ContractStatuses.Terminated) { c.TerminatedOn = e.LastWorkDate; c.TerminationReason = "استقالة"; }
            db.EmploymentContracts.Add(c);
        }
        await db.SaveChangesAsync();

        static EmploymentContract NewContract(Employee e, string number, DateOnly start, DateOnly? end, string status, decimal basic) => new()
        {
            ContractNumber = number, EmployeeId = e.Id, CompanyId = e.CompanyId, BranchId = e.BranchId,
            ContractType = e.Work!.ContractType, StartDate = start, EndDate = end, JobTitleAr = e.JobTitleAr,
            BasicSalary = basic, HousingAllowance = Math.Round(basic * 0.25m), TransportAllowance = Math.Round(basic * 0.10m),
            OtherAllowance = e.Financial!.OtherAllowance, Status = status,
            CreatedAt = start.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
        };
    }

    // ═══ Leaves ═════════════════════════════════════════════════════════════

    private static async Task SeedLeavesAsync(AppDbContext db, List<Employee> employees, string adminId, DateOnly today)
    {
        var working = employees.Where(e => e.Status == EmployeeStatuses.Active).OrderBy(_ => Rng.Next()).ToList();
        var i = 0;
        var femaleNames = SaudiFemale.Select(x => x.Ar).Concat(new[] { "ماريا", "آنا", "جوي", "دانة" }).ToHashSet();
        Employee NextEmp(bool female)
        {
            if (female && working.Skip(i).FirstOrDefault(x => femaleNames.Contains(x.FirstNameAr ?? "")) is { } f)
            {
                working.Remove(f);
                return f;
            }
            return working[i++ % working.Count];
        }

        void Add(string type, int startOffset, int days, string status, string? reason, string? note = null)
        {
            var e = NextEmp(type == LeaveTypes.Maternity);
            var start = today.AddDays(startOffset);
            var end = start.AddDays(days - 1);
            var req = new LeaveRequest
            {
                EmployeeId = e.Id, CompanyId = e.CompanyId, BranchId = e.BranchId, LeaveType = type,
                StartDate = start, EndDate = end, Days = days, Reason = reason, Status = status,
                CreatedByUserId = adminId, CreatedAt = start.AddDays(-Rng.Next(3, 15)).ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc),
            };
            if (status != RequestStatuses.Pending)
            {
                req.ReviewedByUserId = adminId;
                req.ReviewedAt = req.CreatedAt.AddDays(1);
                req.ReviewNote = note;
            }
            if (status == RequestStatuses.Approved)
            {
                if (type == LeaveTypes.Annual)
                {
                    if (e.Work!.AnnualLeaveBalance < days) e.Work.AnnualLeaveBalance = days + Rng.Next(0, 10);
                    e.Work.AnnualLeaveBalance -= days;
                    e.Work.LastLeaveDate = start;
                    req.BalanceDeducted = true;
                }
                else if (type == LeaveTypes.Sick) e.Work!.SickLeaveUsedDays += days;
                if (start <= today && end >= today) e.Status = EmployeeStatuses.Leave;
            }
            db.LeaveRequests.Add(req);
        }

        // History (approved, already taken)
        Add(LeaveTypes.Annual, -40, 10, RequestStatuses.Approved, "إجازة سنوية");
        Add(LeaveTypes.Annual, -33, 7, RequestStatuses.Approved, "سفر عائلي");
        Add(LeaveTypes.Sick, -20, 3, RequestStatuses.Approved, "تقرير طبي");
        Add(LeaveTypes.Sick, -12, 2, RequestStatuses.Approved, "تقرير طبي");
        Add(LeaveTypes.Emergency, -16, 2, RequestStatuses.Approved, "ظرف عائلي");
        Add(LeaveTypes.Unpaid, -25, 4, RequestStatuses.Approved, "إجازة بدون راتب بطلب الموظف");
        Add(LeaveTypes.Marriage, -60, 5, RequestStatuses.Approved, "إجازة زواج");
        Add(LeaveTypes.Exam, -8, 2, RequestStatuses.Approved, "اختبارات جامعية");
        // Ongoing (employee status → leave)
        Add(LeaveTypes.Annual, -3, 14, RequestStatuses.Approved, "إجازة سنوية");
        Add(LeaveTypes.Sick, -1, 5, RequestStatuses.Approved, "عملية جراحية بسيطة");
        Add(LeaveTypes.Maternity, -20, 70, RequestStatuses.Approved, "إجازة وضع");
        // Upcoming approved
        Add(LeaveTypes.Annual, 9, 10, RequestStatuses.Approved, "إجازة سنوية");
        Add(LeaveTypes.Hajj, 25, 10, RequestStatuses.Approved, "أداء فريضة الحج");
        // Pending
        Add(LeaveTypes.Annual, 5, 7, RequestStatuses.Pending, "إجازة سنوية");
        Add(LeaveTypes.Annual, 14, 21, RequestStatuses.Pending, "زيارة الأهل");
        Add(LeaveTypes.Emergency, 1, 3, RequestStatuses.Pending, "ظرف طارئ");
        Add(LeaveTypes.Sick, 0, 2, RequestStatuses.Pending, "مراجعة مستشفى");
        Add(LeaveTypes.Exam, 18, 3, RequestStatuses.Pending, "اختبارات نهائية");
        // Rejected / cancelled
        Add(LeaveTypes.Annual, -5, 12, RequestStatuses.Rejected, "إجازة سنوية", "ضغط عمل في نهاية الربع — يُرجى التأجيل");
        Add(LeaveTypes.Unpaid, 30, 15, RequestStatuses.Rejected, "سفر", "المدة طويلة");
        Add(LeaveTypes.Annual, -15, 5, RequestStatuses.Cancelled, "إجازة سنوية");

        await db.SaveChangesAsync();
    }

    // ═══ Attendance ═════════════════════════════════════════════════════════

    private static async Task SeedAttendanceAsync(AppDbContext db, List<Employee> employees, DateOnly today)
    {
        // Covers the previous full month + this month, so the monthly summary and the payroll absence deduction have data.
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var leaves = await db.LeaveRequests.AsNoTracking().Where(l => l.Status == RequestStatuses.Approved).ToListAsync();
        var start = new TimeOnly(8, 0);

        // A couple of habitual latecomers / absentees make the reports less uniform.
        var lateProne = employees.OrderBy(_ => Rng.Next()).Take(5).Select(e => e.Id).ToHashSet();
        var absentProne = employees.OrderBy(_ => Rng.Next()).Take(3).Select(e => e.Id).ToHashSet();

        var batch = 0;
        for (var d = from; d <= today; d = d.AddDays(1))
        {
            if (HrCalendar.IsWeekend(d)) continue;
            foreach (var e in employees)
            {
                if (e.HireDate > d) continue;
                if (e.LastWorkDate.HasValue && e.LastWorkDate < d) continue;
                if (d == today && Rng.NextDouble() < 0.18) continue;   // today's sheet is still being filled in

                var rec = new AttendanceRecord { EmployeeId = e.Id, CompanyId = e.CompanyId, BranchId = e.BranchId, Date = d };
                var onLeave = leaves.Any(l => l.EmployeeId == e.Id && l.StartDate <= d && l.EndDate >= d);
                var roll = Rng.NextDouble();
                if (onLeave) rec.Status = AttendanceStatuses.Leave;
                else if (roll < (absentProne.Contains(e.Id) ? 0.08 : 0.015)) rec.Status = AttendanceStatuses.Absent;
                else if (roll < (lateProne.Contains(e.Id) ? 0.30 : 0.07))
                {
                    rec.Status = AttendanceStatuses.Late;
                    rec.CheckIn = start.AddMinutes(Rng.Next(17, 75));
                    rec.LateMinutes = (int)(rec.CheckIn.Value - start).TotalMinutes;
                }
                else if (roll < 0.10 && e.JobTitleAr is "أخصائي تقنية معلومات" or "محاسب") { rec.Status = AttendanceStatuses.Remote; rec.CheckIn = start.AddMinutes(Rng.Next(-10, 10)); }
                else { rec.Status = AttendanceStatuses.Present; rec.CheckIn = start.AddMinutes(Rng.Next(-20, 14)); }

                if (rec.CheckIn.HasValue && d < today) rec.CheckOut = rec.CheckIn.Value.AddHours(8).AddMinutes(Rng.Next(0, 40));
                rec.UpdatedAt = d.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc);
                db.AttendanceRecords.Add(rec);
                if (++batch % 1000 == 0) await db.SaveChangesAsync();
            }
        }
        await db.SaveChangesAsync();
    }

    // ═══ Advances ═══════════════════════════════════════════════════════════

    private static async Task SeedAdvancesAsync(AppDbContext db, List<Employee> employees, string adminId, DateOnly today)
    {
        var pool = employees.Where(e => e.Status == EmployeeStatuses.Active && e.BasicSalary >= 3000).OrderBy(_ => Rng.Next()).ToList();
        var i = 0;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        void Add(decimal amount, int installments, int startMonthsAgo, string status, string reason, string? note = null)
        {
            var e = pool[i++];
            var start = thisMonth.AddMonths(-startMonthsAgo);
            var a = new SalaryAdvance
            {
                EmployeeId = e.Id, CompanyId = e.CompanyId, BranchId = e.BranchId,
                Amount = amount, Installments = installments, MonthlyInstallment = Math.Ceiling(amount / installments * 100) / 100,
                StartYear = start.Year, StartMonth = start.Month, Reason = reason, Status = status,
                CreatedByUserId = adminId, CreatedAt = start.AddDays(-12).ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc),
            };
            if (status != RequestStatuses.Pending)
            {
                a.ReviewedByUserId = adminId;
                a.ReviewedAt = a.CreatedAt.AddDays(2);
                a.ReviewNote = note;
            }
            db.SalaryAdvances.Add(a);
        }

        // Approved advances already being repaid through past payroll runs (repayments applied by SeedPayrollAsync).
        Add(6000, 6, 3, RequestStatuses.Approved, "ظروف أسرية");
        Add(4500, 3, 2, RequestStatuses.Approved, "رسوم دراسية");
        Add(9000, 6, 2, RequestStatuses.Approved, "شراء سيارة");
        Add(3000, 2, 3, RequestStatuses.Approved, "علاج");        // fully repaid → closed
        Add(2400, 4, 1, RequestStatuses.Approved, "إيجار سكن");
        Add(5000, 5, -1, RequestStatuses.Pending, "تجهيز منزل");
        Add(3500, 4, -1, RequestStatuses.Pending, "سفر");
        Add(12000, 12, -1, RequestStatuses.Rejected, "قرض شخصي", "يتجاوز سقف السلف المسموح");
        await db.SaveChangesAsync();
    }

    // ═══ Payroll ════════════════════════════════════════════════════════════

    private static async Task SeedPayrollAsync(AppDbContext db, List<Company> companies, string adminId, DateOnly today)
    {
        var calc = new PayrollCalculator(db);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        // Three closed months (paid, paid, approved) per company + a draft of the current month for the first company.
        for (var back = 3; back >= 1; back--)
        {
            var p = thisMonth.AddMonths(-back);
            foreach (var c in companies)
            {
                var run = new PayrollRun { CompanyId = c.Id, Year = p.Year, Month = p.Month, CreatedByUserId = adminId,
                                           CreatedAt = PayrollCalculator.MonthEnd(p.Year, p.Month).AddDays(-3).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc) };
                db.PayrollRuns.Add(run);
                await calc.BuildItemsAsync(run);

                // A few bonuses / manual deductions so the sheet isn't uniform.
                foreach (var item in run.Items.OrderBy(_ => Rng.Next()).Take(Math.Max(1, run.Items.Count / 8)))
                {
                    if (Rng.NextDouble() < 0.6) { item.Additions = RoundTo(Rng.Next(300, 1500), 50); item.Notes = "عمل إضافي"; }
                    else { item.OtherDeductions = RoundTo(Rng.Next(100, 400), 50); item.Notes = "مخالفة تأخير متكرر"; }
                }
                PayrollCalculator.RecalcTotals(run);
                await db.SaveChangesAsync();

                await calc.ApplyAdvanceRepaymentsAsync(run);
                run.Status = back == 1 ? PayrollStatuses.Approved : PayrollStatuses.Paid;
                run.ApprovedByUserId = adminId;
                run.ApprovedAt = run.CreatedAt.AddDays(1);
                if (run.Status == PayrollStatuses.Paid) run.PaidAt = run.CreatedAt.AddDays(2);
                await db.SaveChangesAsync();
            }
        }

        var draft = new PayrollRun { CompanyId = companies[0].Id, Year = thisMonth.Year, Month = thisMonth.Month, CreatedByUserId = adminId,
                                     Notes = "مسودة الشهر الحالي — قابلة للتعديل" };
        db.PayrollRuns.Add(draft);
        await calc.BuildItemsAsync(draft);
        await db.SaveChangesAsync();
    }

    private static async Task UpdateCountersAsync(AppDbContext db)
    {
        foreach (var c in await db.Companies.ToListAsync())
        {
            c.EmployeesCount = await db.Employees.CountAsync(e => e.CompanyId == c.Id && (e.Status == EmployeeStatuses.Active || e.Status == EmployeeStatuses.Leave));
            c.BranchesCount = await db.Branches.CountAsync(b => b.CompanyId == c.Id);
        }
        foreach (var b in await db.Branches.ToListAsync())
            b.EmployeesCount = await db.Employees.CountAsync(e => e.BranchId == b.Id && (e.Status == EmployeeStatuses.Active || e.Status == EmployeeStatuses.Leave));
        await db.SaveChangesAsync();
    }

    // ═══ helpers ════════════════════════════════════════════════════════════

    private static T Pick<T>(IReadOnlyList<T> xs) => xs[Rng.Next(xs.Count)];

    private static (string Code, (string Ar, string En)[] First, (string Ar, string En)[] Family, int Weight) PickWeighted(
        (string Code, (string Ar, string En)[] First, (string Ar, string En)[] Family, int Weight)[] xs)
    {
        var roll = Rng.Next(xs.Sum(x => x.Weight));
        foreach (var x in xs) { if (roll < x.Weight) return x; roll -= x.Weight; }
        return xs[0];
    }

    private static string Digits(int n) => string.Concat(Enumerable.Range(0, n).Select(_ => (char)('0' + Rng.Next(10))));
    private static string Letters(int n) => string.Concat(Enumerable.Range(0, n).Select(_ => (char)('A' + Rng.Next(26))));
    private static decimal RoundTo(int v, int step) => (decimal)(v / step * step);
    private static string Slug(string s) => new string(s.ToLowerInvariant().Where(char.IsLetter).ToArray());

    private static string Initials(string nameAr)
    {
        var parts = nameAr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch { 0 => "؟", 1 => parts[0][..1], _ => $"{parts[0][..1]}{parts[^1].Replace("ال", "")[..1]}" };
    }
}
