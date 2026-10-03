using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;

namespace Project_HR_1.Services.Import;

// Imports the company's own "قاعدة بيانات الموظفين" sheet (the 42-column export: رقم الموظف،
// اسم الموظف، الجنسية، رقم الهوية، الاداره، جهة العمل، الكفيل ...) directly — no template needed.
// Always run twice: once with commit=false (preview/dry run), then commit=true after the user confirms.
// See docs/features/employee-sheet-import.md for every rule applied here.

public enum SheetImportMode { SkipExisting = 0, UpdateExisting = 1 }

public class SheetImportIssue
{
    public int Row { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Level { get; set; } = "warning"; // "error" (row not imported) | "warning" (imported, review it)
    public string Category { get; set; } = "";      // short stable label used to group issues on the preview page
    public string Message { get; set; } = "";
}

public class SheetImportReport
{
    public string? Token { get; set; }
    public string FileName { get; set; } = "";
    public string SheetName { get; set; } = "";
    public SheetImportMode Mode { get; set; }
    public bool Committed { get; set; }
    public string? FatalError { get; set; }

    public int TotalRows { get; set; }
    public int ToCreate { get; set; }
    public int ToUpdate { get; set; }
    public int SkippedExisting { get; set; }
    public int ErrorRows { get; set; }
    public int DuplicatesMerged { get; set; }
    public int SaudiCount { get; set; }
    public int ForeignCount { get; set; }

    public List<string> NewCompanies { get; set; } = new();
    public List<string> NewBranches { get; set; } = new();   // "الشركة ← الفرع"
    public List<string> NewSponsors { get; set; } = new();   // "الشركة ← الكفيل"

    // Automatic corrections, aggregated: description -> count (+ a few examples).
    public Dictionary<string, int> Fixes { get; set; } = new();
    public Dictionary<string, List<string>> FixExamples { get; set; } = new();

    public List<SheetImportIssue> Issues { get; set; } = new();

    public void Fix(string what, string example)
    {
        Fixes[what] = Fixes.GetValueOrDefault(what) + 1;
        if (!FixExamples.TryGetValue(what, out var list)) FixExamples[what] = list = new List<string>();
        if (list.Count < 4 && !list.Contains(example)) list.Add(example);
    }
}

public class EmployeeSheetImporter
{
    private readonly AppDbContext _db;
    public EmployeeSheetImporter(AppDbContext db) => _db = db;

    // ── Column aliases (matched after Norm(), so spacing/hamza/ta-marbuta differences don't matter) ──
    private static readonly Dictionary<string, string[]> ColumnAliases = new()
    {
        ["code"]        = new[] { "رقم الموظف", "رمز الموظف", "الرقم الوظيفي" },
        ["name"]        = new[] { "اسم الموظف", "الاسم الكامل", "الاسم" },
        ["nationality"] = new[] { "الجنسية" },
        ["id"]          = new[] { "رقم الهوية", "رقم الهوية / الإقامة", "رقم الاقامة", "رقم الهوية/الإقامة" },
        ["idExpiry"]    = new[] { "تاريخ انتهاء الهوية", "انتهاء الهوية" },
        ["education"]   = new[] { "المؤهل العلمي", "الحالة التعليمية" },
        ["job"]         = new[] { "المهنة", "المسمى الوظيفي" },
        ["contractType"]= new[] { "نوع العقد" },
        ["branch"]      = new[] { "الاداره", "الإدارة", "الفرع", "الفرع (الإدارة)" },
        ["company"]     = new[] { "جهة العمل", "الشركة", "الشركة (جهة العمل)" },
        ["sponsor"]     = new[] { "الكفيل", "الكفيل (خاص بالرواتب)", "جهة استلام راتبه" },
        ["hireDate"]    = new[] { "تاريخ الالتحاق", "تاريخ التعيين" },
        ["lastWork"]    = new[] { "تاريخ آخر مباشرة", "تاريخ اخر مباشرة" },
        ["contractStart"] = new[] { "بداية العقد", "بداية لبعقد" },
        ["contractEnd"] = new[] { "نهاية العقد" },
        ["hours"]       = new[] { "ساعات الدوام" },
        ["basic"]       = new[] { "الراتب الأساسي", "الراتب الاساسي" },
        ["allowances"]  = new[] { "البدلات" },
        ["transport"]   = new[] { "بدل نقل", "بدل النقل" },
        ["otherAllow"]  = new[] { "بدلات أخرى", "بدلات اخرى" },
        ["housing"]     = new[] { "بدل السكن الشهري", "بدل السكن" },
        ["bank"]        = new[] { "اسم البنك", "البنك" },
        ["iban"]        = new[] { "رقم الحساب البنكي", "رقم الآيبان (IBAN)", "الآيبان", "IBAN" },
        ["sickDays"]    = new[] { "أيام الاجازات المرضيه", "أيام الإجازات المرضية" },
        ["lastLeave"]   = new[] { "اخر إجازة", "آخر إجازة" },
        ["marital"]     = new[] { "الإحالة الاجتماعية", "الحالة الاجتماعية" },
        ["children"]    = new[] { "عدد أولاد", "عدد الأولاد" },
        ["phone"]       = new[] { "رقم الجوال", "الجوال" },
        ["email"]       = new[] { "البريد الالكتروني", "البريد الإلكتروني", "Email" },
    };

    // GOSI exports strip the spaces out of occupations ("مشغلالعاب"). Known ones are restored here.
    private static readonly Dictionary<string, string> JobFixes = new()
    {
        ["مشغلالعاب"] = "مشغل ألعاب", ["كاتباتصالات"] = "كاتب اتصالات", ["طبيباسنانعام"] = "طبيب أسنان عام",
        ["مراقبأعمال"] = "مراقب أعمال", ["طبيبةجلدية"] = "طبيبة جلدية", ["بائعه"] = "بائعة",
        ["سائقعموممركبات"] = "سائق عموم مركبات", ["مشرفأعمال"] = "مشرف أعمال", ["محاسبعام"] = "محاسب عام",
        ["مندوبعلاقاتعامة"] = "مندوب علاقات عامة", ["مبرمجكمبيوتر"] = "مبرمج كمبيوتر", ["مراقبعمال"] = "مراقب عمال",
        ["مندوبمبيعات"] = "مندوب مبيعات", ["كاتبملفات"] = "كاتب ملفات", ["مشرفامن"] = "مشرف أمن",
        ["مشرفعمال"] = "مشرف عمال", ["فنيصيانة"] = "فني صيانة", ["مديرإداري"] = "مدير إداري", ["مديرعام"] = "مدير عام",
        ["نائبالمديرالعام"] = "نائب المدير العام", ["مهندسكمبيوتر"] = "مهندس كمبيوتر", ["مديرمشتريات"] = "مدير مشتريات",
        ["مندوبمشتريات"] = "مندوب مشتريات", ["سكرتيرإداري"] = "سكرتير إداري", ["فنيمعمل"] = "فني معمل", ["امن"] = "أمن",
        ["عاملنظافة"] = "عامل نظافة", ["حارسامن"] = "حارس أمن", ["مساعدطبيب"] = "مساعد طبيب", ["فنياسنان"] = "فني أسنان",
        ["مديرفرع"] = "مدير فرع", ["مديرمبيعات"] = "مدير مبيعات",
    };

    private static readonly Dictionary<string, string> NationalityAliases = new()
    {
        ["مصر"] = "EG", ["نيجيريا"] = "NG", ["نيجيري"] = "NG", ["النيجر"] = "NE", ["نيجر"] = "NE",
        ["ارتيري"] = "ER", ["اريتري"] = "ER", ["الهند"] = "IN", ["اليمن"] = "YE", ["السودان"] = "SD",
        ["سوريا"] = "SY", ["الاردن"] = "JO", ["باكستان"] = "PK", ["بنغلاديش"] = "BD", ["بنجلاديشي"] = "BD",
        ["الفلبين"] = "PH", ["اندونيسيا"] = "ID", ["نيبال"] = "NP", ["سعوديه"] = "SA", ["السعوديه"] = "SA",
    };

    private sealed class Row
    {
        public int RowNo;
        public string Code = "", Name = "";
        public string CompanyRaw = "", BranchRaw = "", SponsorRaw = "";
        public string CompanyOrig = "", SponsorOrig = "";   // exactly as written in the sheet
        public Dictionary<string, object?> Cells = new();
    }

    public async Task<SheetImportReport> RunAsync(Stream xlsx, string fileName, SheetImportMode mode, bool commit,
        int? scopeCompanyId, int? scopeBranchId, string? userId)
    {
        var rep = new SheetImportReport { FileName = fileName, Mode = mode };

        using var pkg = new ExcelPackage(xlsx);
        var (ws, headerRow, cols) = FindSheet(pkg);
        if (ws == null)
        {
            rep.FatalError = "لم أجد في الملف ورقة فيها عمودا \"رقم الموظف\" و\"اسم الموظف\". تأكد أنك رفعت كشف الموظفين الصحيح.";
            return rep;
        }
        rep.SheetName = ws.Name;
        foreach (var req in new[] { "code", "name", "company" })
            if (!cols.ContainsKey(req))
            {
                rep.FatalError = $"الملف ينقصه العمود الإلزامي: {ColumnAliases[req][0]}";
                return rep;
            }

        // ── Pass 1: read rows ──────────────────────────────────────────────
        var rows = new List<Row>();
        int lastRow = ws.Dimension?.End.Row ?? headerRow;
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var row = new Row { RowNo = r };
            foreach (var (key, c) in cols) row.Cells[key] = ws.Cells[r, c].Value;
            row.Code = Str(row.Cells["code"]);
            row.Name = CleanSpaces(Str(row.Cells["name"]));
            if (row.Code == "" && row.Name == "") continue;
            row.CompanyRaw = CleanSpaces(Str(row.Cells["company"]));
            row.BranchRaw = CleanSpaces(Get(row, "branch"));
            row.SponsorRaw = CleanSpaces(Get(row, "sponsor"));

            // Fold every spelling / sub-entity into the group's official company names (CompanyNames.cs).
            // The original text is kept: it decides WHICH existing company/division the row goes to.
            row.CompanyOrig = row.CompanyRaw; row.SponsorOrig = row.SponsorRaw;
            row.CompanyRaw = CompanyNames.Canonicalize(row.CompanyRaw);
            row.SponsorRaw = CompanyNames.Canonicalize(row.SponsorRaw);
            rows.Add(row);
        }
        rep.TotalRows = rows.Count;

        rows = MergeDuplicates(rows, rep);

        // Pick one display spelling per normalized company/branch/sponsor name (the most frequent).
        string Pick(IEnumerable<string> variants) => variants.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Length).First().Key;
        var companyDisplay = rows.Where(r => r.CompanyRaw != "").GroupBy(r => CompanyKey(r.CompanyRaw))
            .ToDictionary(g => g.Key, g => Pick(g.Select(r => StripTrailingDigits(r.CompanyRaw))));

        // ── Load existing data ─────────────────────────────────────────────
        IQueryable<Company> coQ = _db.Companies; IQueryable<Branch> brQ = _db.Branches; IQueryable<Sponsor> spQ = _db.Sponsors;
        IQueryable<Employee> empQ = _db.Employees.Include(e => e.Personal).Include(e => e.Financial).Include(e => e.Work);
        if (!commit) { coQ = coQ.AsNoTracking(); brQ = brQ.AsNoTracking(); spQ = spQ.AsNoTracking(); empQ = empQ.AsNoTracking(); }

        // Companies/branches the owner already created in the system are the TARGET — rows are attached to them,
        // new ones are only created when nothing in the system matches.
        var dbCompanies = await coQ.ToListAsync();
        var companies = new Dictionary<string, Company>();   // canonical key -> company created by this import
        var branches = new Dictionary<(int? co, string key), Branch>();
        var sponsors = new Dictionary<(int? co, string key), Sponsor>();
        var newBranchByCompany = new Dictionary<(Company co, string key), Branch>();
        var newSponsorByCompany = new Dictionary<(Company co, string key), Sponsor>();
        var dbBranches = await brQ.ToListAsync();
        foreach (var b in dbBranches) branches.TryAdd((b.CompanyId, BranchKey(b.NameAr)), b);
        foreach (var s in await spQ.ToListAsync()) sponsors.TryAdd((s.CompanyId, Norm(s.NameAr)), s);

        var existing = await empQ.ToListAsync();
        var byCode = existing.GroupBy(e => e.EmployeeCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var idOwner = new Dictionary<string, string>();
        foreach (var e in existing)
            foreach (var id in new[] { e.NationalId, e.IqamaNumber })
                if (!string.IsNullOrEmpty(id)) idOwner.TryAdd(id, e.EmployeeCode);
        var byName = existing.GroupBy(e => Norm(e.NameAr)).ToDictionary(g => g.Key, g => g.ToList());
        var emailOwner = existing.Where(e => !string.IsNullOrEmpty(e.Email))
            .GroupBy(e => e.Email!.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().EmployeeCode);

        // Existing company for a raw sheet name: same official group (CompanyNames), and when the group has
        // several divisions in the system (e.g. شركة الأفق للتجارة: التجزئة / الجملة) the division keyword decides.
        Company? FindDbCompany(string orig)
        {
            if (orig == "") return null;
            var canonKey = CompanyKey(CompanyNames.Canonicalize(orig));
            var cands = dbCompanies.Where(c => CompanyKey(CompanyNames.Canonicalize(c.NameAr)) == canonKey).OrderBy(c => c.Id).ToList();
            if (cands.Count <= 1) return cands.FirstOrDefault();
            var division = CompanyNames.Division(orig);
            return (division == null ? null : cands.FirstOrDefault(c => Norm(c.NameAr).Contains(division))) ?? cands[0];
        }

        Company? GetCompany(Row r, List<string> errors)
        {
            var db = FindDbCompany(r.CompanyOrig);
            if (db != null)
            {
                if (Norm(db.NameAr) != Norm(r.CompanyOrig)) rep.Fix("ربط جهة العمل بالشركة المسجلة في النظام", $"{r.CompanyOrig} ← {db.NameAr}");
                return db;
            }
            var key = CompanyKey(r.CompanyRaw);
            if (companies.TryGetValue(key, out var c)) return c;
            if (scopeCompanyId.HasValue) { errors.Add($"الشركة '{r.CompanyRaw}' غير موجودة ضمن نطاق صلاحيتك الحالي — غيّر النطاق إلى \"كل الشركات\" لإضافتها."); return null; }
            var name = companyDisplay.GetValueOrDefault(key, r.CompanyRaw);
            c = new Company { NameAr = name, NameEn = name, ShortNameAr = name, Slug = NewSlug("co"), Initials = OrgInitials(name) };
            if (commit) _db.Companies.Add(c);
            companies[key] = c;
            rep.NewCompanies.Add(name);
            return c;
        }

        Branch GetBranch(Company co, string raw)
        {
            var name = raw == "" ? "فرع غير محدد" : raw;
            var key = BranchKey(name);
            if (co.Id != 0 && branches.TryGetValue((co.Id, key), out var b)) return b;
            // The branch may already exist under another division of the same group (e.g. another division of the same company):
            // if exactly one such branch exists, use it (the employee's company follows the branch).
            if (raw != "")
            {
                var group = CompanyKey(CompanyNames.Canonicalize(co.NameAr));
                var elsewhere = dbBranches.Where(x => BranchKey(x.NameAr) == key && dbCompanies.Any(c => c.Id == x.CompanyId
                    && CompanyKey(CompanyNames.Canonicalize(c.NameAr)) == group)).ToList();
                if (elsewhere.Count == 1) return elsewhere[0];
            }
            if (newBranchByCompany.TryGetValue((co, key), out b)) return b;
            b = new Branch { NameAr = name, NameEn = name, Slug = NewSlug("br"), Company = co, CompanyId = co.Id };
            if (commit) _db.Branches.Add(b);
            newBranchByCompany[(co, key)] = b;
            rep.NewBranches.Add($"{co.NameAr} ← {name}");
            return b;
        }

        Sponsor? GetSponsor(Company co, string orig)
        {
            if (orig == "") return null;
            // Sponsors use the same names as the companies registered in the system (owner decision).
            var raw = FindDbCompany(orig)?.NameAr ?? CompanyNames.Canonicalize(orig);
            if (Norm(raw) != Norm(orig)) rep.Fix("توحيد اسم الكفيل", $"{orig} ← {raw}");
            var key = Norm(raw);
            if (co.Id != 0 && sponsors.TryGetValue((co.Id, key), out var s)) return s;
            if (newSponsorByCompany.TryGetValue((co, key), out s)) return s;
            var name = raw;
            s = new Sponsor { NameAr = name, NameEn = name, Company = co, CompanyId = co.Id };
            if (commit) _db.Sponsors.Add(s);
            newSponsorByCompany[(co, key)] = s;
            rep.NewSponsors.Add($"{co.NameAr} ← {name}");
            return s;
        }

        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new Dictionary<string, string>();
        var seenEmails = new HashSet<string>();

        // ── Pass 2: validate + map each row ────────────────────────────────
        foreach (var r in rows)
        {
            var errors = new List<string>();
            var warnings = new List<(string Cat, string Msg)>();
            void Issue(string level, string msg, string cat = "") => rep.Issues.Add(new SheetImportIssue { Row = r.RowNo, Code = r.Code, Name = r.Name, Level = level, Message = msg, Category = cat == "" ? msg : cat });

            if (r.Code == "") errors.Add("رقم الموظف فارغ");
            if (r.Name == "") errors.Add("اسم الموظف فارغ");
            if (r.CompanyRaw == "") errors.Add("جهة العمل (الشركة) فارغة");
            if (r.Code != "" && !seenCodes.Add(r.Code)) errors.Add($"رقم الموظف {r.Code} مكرر داخل الملف — تم اعتماد أول صف فقط");

            // National ID / Iqama
            var idRaw = Regex.Replace(Get(r, "id"), @"\D", "");
            if (idRaw != "" && seenIds.TryGetValue(idRaw, out var firstCode))
                errors.Add($"رقم الهوية {idRaw} مكرر في الملف (نفس الشخص برقم الموظف {firstCode}) — تم اعتماد الصف الأول فقط");

            // Same person already in the system? Match by employee code, then ID/Iqama, then (only when the
            // row has no ID) by exact name. A match is never duplicated: it's skipped or updated per `mode`.
            byCode.TryGetValue(r.Code, out var current);
            if (current == null && idRaw != "" && idOwner.TryGetValue(idRaw, out var idCode))
            {
                byCode.TryGetValue(idCode, out current);
                if (current != null) warnings.Add(("الموظف موجود مسبقاً برقم موظف مختلف", $"نفس رقم الهوية {idRaw} موجود في النظام برقم الموظف {idCode} — لم يُضف كموظف جديد"));
            }
            if (current == null && idRaw == "" && byName.TryGetValue(Norm(r.Name), out var sameName) && sameName.Count == 1)
            {
                current = sameName[0];
                warnings.Add(("الموظف موجود مسبقاً بنفس الاسم", $"يوجد في النظام موظف بنفس الاسم (رقم {current.EmployeeCode}) وهذا الصف بدون رقم هوية — اعتُبر نفس الشخص"));
            }
            if (errors.Count == 0 && current != null && mode == SheetImportMode.SkipExisting)
            {
                rep.SkippedExisting++;
                if (idRaw != "") seenIds.TryAdd(idRaw, r.Code);
                foreach (var (cat, msg) in warnings) Issue("warning", msg, cat);
                continue;
            }

            Company? company = errors.Count == 0 ? GetCompany(r, errors) : null;
            if (errors.Count > 0)
            {
                rep.ErrorRows++;
                foreach (var e in errors) Issue("error", e);
                continue;
            }
            if (idRaw != "") seenIds.TryAdd(idRaw, r.Code);

            var branch = GetBranch(company!, r.BranchRaw);
            if (branch.CompanyId != 0 && company!.Id != branch.CompanyId)
            {
                var branchOwner = dbCompanies.First(c => c.Id == branch.CompanyId);
                rep.Fix("نقل الموظف للشركة التي يتبعها فرعه في النظام", $"{r.BranchRaw}: {company.NameAr} ← {branchOwner.NameAr}");
                company = branchOwner;
            }
            if (r.BranchRaw == "") warnings.Add(("فرع فارغ — وُضع في \"فرع غير محدد\"", "الفرع (الإدارة) فارغ — وُضع في \"فرع غير محدد\"، انقله للفرع الصحيح لاحقاً"));
            if (scopeBranchId.HasValue && branch.Id != scopeBranchId.Value)
            {
                rep.ErrorRows++; Issue("error", "الفرع خارج نطاق صلاحيتك الحالي"); continue;
            }
            var sponsor = GetSponsor(company!, r.SponsorOrig);

            // Nationality
            var natRaw = Get(r, "nationality");
            var nat = MapNationality(natRaw);
            if (nat != null && Norm(natRaw) != Norm(nat.NameAr)) rep.Fix("توحيد اسم الجنسية", $"{natRaw} ← {nat.NameAr}");
            if (nat == null && natRaw != "") warnings.Add(("جنسية غير معروفة — حُفظت كـ \"أخرى\"", $"جنسية غير معروفة '{natRaw}' — حُفظت كـ \"أخرى\""));
            if (natRaw == "") warnings.Add(("الجنسية فارغة", "الجنسية فارغة"));
            var isSaudi = nat?.Code == "SA";
            if (nat != null) { if (isSaudi) rep.SaudiCount++; else rep.ForeignCount++; }

            // ID type follows the number itself: 1xxxxxxxxx = هوية وطنية, 2xxxxxxxxx = إقامة.
            string? nationalId = null, iqama = null;
            if (idRaw == "") warnings.Add(("لا يوجد رقم هوية/إقامة", "لا يوجد رقم هوية/إقامة"));
            else
            {
                if (idRaw.StartsWith("1")) nationalId = idRaw; else iqama = idRaw;
                if (idRaw.Length != 10) warnings.Add(("طول رقم الهوية/الإقامة غير صحيح", $"رقم الهوية/الإقامة {idRaw} طوله {idRaw.Length} خانات (المفروض 10)"));
                if (nat != null && isSaudi && !idRaw.StartsWith("1")) warnings.Add(("جنسية سعودي مع رقم إقامة", $"الجنسية سعودي لكن الرقم {idRaw} رقم إقامة (يبدأ بـ 2) — راجع الجنسية"));
                if (nat != null && !isSaudi && idRaw.StartsWith("1")) warnings.Add(("جنسية غير سعودي مع رقم هوية وطنية", $"الجنسية {nat.NameAr} لكن الرقم {idRaw} رقم هوية وطنية (يبدأ بـ 1) — راجع الجنسية"));
                if (idOwner.TryGetValue(idRaw, out var owner) && !owner.Equals(r.Code, StringComparison.OrdinalIgnoreCase) && !owner.Equals(current?.EmployeeCode, StringComparison.OrdinalIgnoreCase))
                    warnings.Add(("رقم الهوية مسجل لموظف آخر في النظام", $"رقم الهوية {idRaw} مسجل مسبقاً في النظام لموظف آخر (رقم {owner})"));
            }
            var idExpiry = ParseDate(r.Cells.GetValueOrDefault("idExpiry"), out var wasHijri);
            if (idExpiry.HasValue && wasHijri) rep.Fix("تحويل تاريخ انتهاء الهوية من هجري إلى ميلادي", $"{Get(r, "idExpiry")} ← {idExpiry:yyyy/MM/dd}");

            // Job title
            var jobRaw = Get(r, "job");
            var job = jobRaw == "" ? null : (JobFixes.TryGetValue(jobRaw.Replace(" ", ""), out var fixedJob) ? fixedJob : jobRaw);
            if (job != null && job != jobRaw) rep.Fix("إصلاح المسميات الملتصقة (بدون مسافات)", $"{jobRaw} ← {job}");

            // Phone
            var phone = NormalizePhone(Get(r, "phone"), out var phoneFixed);
            if (phone != null && phoneFixed) rep.Fix("تصحيح رقم الجوال لصيغة 05xxxxxxxx", $"{Get(r, "phone")} ← {phone}");
            if (phone == null && Get(r, "phone") != "") warnings.Add(("رقم جوال غير صالح — لم يُحفظ", $"رقم الجوال '{Get(r, "phone")}' غير صالح — لم يُحفظ"));

            // Email (stored on the employee; no login account is created by this import)
            var email = Get(r, "email").ToLowerInvariant();
            if (email != "")
            {
                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) { warnings.Add(("بريد غير صالح — لم يُحفظ", $"البريد '{email}' غير صالح — لم يُحفظ")); email = ""; }
                else if ((emailOwner.TryGetValue(email, out var eo) && !eo.Equals(r.Code, StringComparison.OrdinalIgnoreCase)) || !seenEmails.Add(email))
                { warnings.Add(("بريد مستخدم لموظف آخر — لم يُحفظ", $"البريد '{email}' مستخدم لموظف آخر — لم يُحفظ")); email = ""; }
            }

            // Bank / IBAN
            var bankRaw = Get(r, "bank");
            var bank = MapBank(bankRaw);
            if (bank != null && bank != bankRaw) rep.Fix("توحيد أسماء البنوك", $"{bankRaw} ← {bank}");
            var ibanRaw = Get(r, "iban");
            var iban = ibanRaw == "" ? null : Regex.Replace(ibanRaw, @"\s", "").ToUpperInvariant();
            if (iban != null && iban != ibanRaw) rep.Fix("تحويل الآيبان لأحرف كبيرة/حذف المسافات", $"{ibanRaw} ← {iban}");
            if (iban != null && !Regex.IsMatch(iban, @"^SA\d{22}$")) warnings.Add(("آيبان ناقص أو بصيغة خاطئة", $"الآيبان '{iban}' غير مكتمل أو بصيغة خاطئة (المفروض SA + 22 رقم) — حُفظ كما هو، صحّحه"));
            if (iban != null && iban.Length > 34) iban = iban[..34];

            // Money / numbers / dates
            var basic = Dec(r, "basic");
            var housing = Dec(r, "housing");
            var transport = Dec(r, "transport");
            var allowances = Dec(r, "allowances") ?? 0;
            var otherAllow = Dec(r, "otherAllow") ?? 0;
            var hire = ParseDate(r.Cells.GetValueOrDefault("hireDate"), out _);
            var lastWork = ParseDate(r.Cells.GetValueOrDefault("lastWork"), out _);
            var cStart = ParseDate(r.Cells.GetValueOrDefault("contractStart"), out _);
            var cEnd = ParseDate(r.Cells.GetValueOrDefault("contractEnd"), out _);
            var lastLeave = ParseDate(r.Cells.GetValueOrDefault("lastLeave"), out _);
            if (hire == null) warnings.Add(("تاريخ الالتحاق فارغ", "تاريخ الالتحاق فارغ أو غير مفهوم"));
            var marital = MapMarital(Get(r, "marital"));
            var eduRaw = Get(r, "education");

            // ── Apply ──
            var isNew = current == null;
            var emp = current ?? new Employee
            {
                EmployeeCode = r.Code, Status = "active", AvatarColor = "#1a2744",
                CreatedAt = DateTime.UtcNow, CreatedByUserId = userId,
            };
            emp.Personal ??= new EmployeePersonal();
            emp.Financial ??= new EmployeeFinancial();
            emp.Work ??= new EmployeeWork { WorkSystem = WorkSystem.FullTime, ProbationDays = 90 };

            var (first, father, grand, family) = SplitName(r.Name);
            emp.NameAr = Trunc(r.Name, 200); emp.NameEn = emp.NameAr; emp.Initials = Initials(r.Name);
            emp.FirstNameAr = first; emp.FatherNameAr = father; emp.GrandfatherNameAr = grand; emp.FamilyNameAr = family;

            // Navigations (not raw ids) so brand-new companies/branches/sponsors get their FK fixed up on save.
            emp.Company = company; emp.Branch = branch;
            if (sponsor != null) emp.Sponsor = sponsor;

            if (nat != null) { emp.NationalityCode = nat.Code; emp.NationalityAr = nat.NameAr; emp.NationalityEn = nat.NameEn; }
            else if (natRaw != "") { emp.NationalityCode = "OTH"; emp.NationalityAr = Trunc(natRaw, 100); emp.NationalityEn = null; }
            if (nationalId != null) { emp.NationalId = nationalId; emp.NationalIdExpiry = idExpiry ?? emp.NationalIdExpiry; }
            if (iqama != null) { emp.IqamaNumber = Trunc(iqama, 20); emp.IqamaExpiry = idExpiry ?? emp.IqamaExpiry; }
            if (job != null) { emp.JobTitleAr = Trunc(job, 160); emp.JobTitleEn = emp.JobTitleAr; }
            if (hire != null) emp.HireDate = hire;
            if (lastWork != null) emp.LastWorkDate = lastWork;
            if (cStart != null) emp.ContractStartDate = cStart;
            if (cEnd != null) emp.ContractEndDate = cEnd;
            if (basic != null) emp.BasicSalary = basic.Value;
            if (phone != null) emp.Phone = phone;
            if (email != "") emp.Email = email;

            if (marital != null) emp.Personal.MaritalStatus = marital;
            var kids = Int(r, "children"); if (kids is > 0 || isNew) emp.Personal.ChildrenCount = kids;
            if (eduRaw != "") { emp.Personal.IsEducated = true; emp.Personal.EducationLevel = Trunc(eduRaw, 40); }

            if (bank != null) emp.Financial.BankName = bank;
            if (iban != null) emp.Financial.Iban = iban;
            if (housing != null) emp.Financial.HousingAllowance = housing.Value;
            if (transport != null) emp.Financial.TransportAllowance = transport.Value;
            if (allowances + otherAllow > 0 || isNew)
            {
                emp.Financial.OtherAllowance = allowances + otherAllow;
                emp.Financial.OtherAllowanceDescription = allowances + otherAllow > 0 ? "عمود \"البدلات\" + \"بدلات أخرى\" من كشف الموظفين" : null;
            }
            emp.Financial.SalaryType = SalaryType.Monthly;

            var hours = Dec(r, "hours"); if (hours != null) emp.Work.DailyWorkHours = hours;
            var ct = Get(r, "contractType");
            if (ct != "") emp.Work.ContractType = ct.Contains("دائم") || ct.Contains("مفتوح") ? ContractType.Unlimited : ContractType.Limited;
            else if (cEnd != null) emp.Work.ContractType = ContractType.Limited;
            var sick = Int(r, "sickDays"); if (sick != null) emp.Work.SickLeaveUsedDays = sick.Value;
            if (lastLeave != null) emp.Work.LastLeaveDate = lastLeave;

            if (isNew)
            {
                rep.ToCreate++;
                if (commit) _db.Employees.Add(emp);
                byCode[r.Code] = emp;
            }
            else rep.ToUpdate++;

            foreach (var (cat, msg) in warnings) Issue("warning", msg, cat);
        }

        if (commit && rep.FatalError == null)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            rep.Committed = true;
        }
        return rep;
    }

    // ── In-file duplicates ────────────────────────────────────────────────
    // Same person = same ID/Iqama, or same name (ignoring spaces/hamza) when at least one of the rows has no ID.
    // Two rows with the same name but DIFFERENT IDs are different people and are both kept.
    // The most complete row wins; its empty cells are then filled from the dropped row, so nothing is lost.
    private static List<Row> MergeDuplicates(List<Row> rows, SheetImportReport rep)
    {
        static string Id(Row r) => Regex.Replace(Str(r.Cells.GetValueOrDefault("id")), @"\D", "");
        static int Score(Row r) => r.Cells.Values.Count(v => v != null && Str(v) != "" && Str(v) != "0") + (Id(r) != "" ? 5 : 0);

        var groups = new List<List<Row>>();
        var byId = new Dictionary<string, List<Row>>();
        foreach (var r in rows)
        {
            var id = Id(r);
            if (id != "" && byId.TryGetValue(id, out var g)) { g.Add(r); continue; }
            var nameKey = Norm(r.Name);
            // name match: only against a group that doesn't already hold a different ID
            var byName = nameKey == "" ? null : groups.FirstOrDefault(gr => Norm(gr[0].Name) == nameKey
                && (id == "" || gr.All(x => Id(x) == "" || Id(x) == id)));
            if (byName != null) { byName.Add(r); if (id != "") byId[id] = byName; continue; }
            var ng = new List<Row> { r };
            groups.Add(ng);
            if (id != "") byId[id] = ng;
        }

        var result = new List<Row>();
        foreach (var g in groups)
        {
            if (g.Count == 1) { result.Add(g[0]); continue; }
            var keep = g.OrderByDescending(Score).ThenBy(r => r.RowNo).First();
            foreach (var drop in g.Where(r => r != keep))
            {
                foreach (var (k, v) in drop.Cells)
                    if (v != null && Str(v) != "" && (!keep.Cells.TryGetValue(k, out var kv) || kv == null || Str(kv) == ""))
                        keep.Cells[k] = v;
                if (keep.BranchRaw == "" && drop.BranchRaw != "") keep.BranchRaw = drop.BranchRaw;
                if (keep.SponsorRaw == "" && drop.SponsorRaw != "") keep.SponsorRaw = drop.SponsorRaw;
                rep.DuplicatesMerged++;
                rep.Issues.Add(new SheetImportIssue
                {
                    Row = drop.RowNo, Code = drop.Code, Name = drop.Name, Level = "duplicate", Category = "موظف مكرر — اعتُمد الصف الأكمل",
                    Message = $"مكرر مع الصف {keep.RowNo} (رقم الموظف {keep.Code}) — اعتُمد الصف {keep.RowNo} لأن بياناته أكمل، وأُكملت منه أي خانة ناقصة، وحُذف هذا الصف",
                });
            }
            result.Add(keep);
        }
        return result.OrderBy(r => r.RowNo).ToList();
    }

    // ── Sheet discovery ───────────────────────────────────────────────────
    private static (ExcelWorksheet? ws, int headerRow, Dictionary<string, int> cols) FindSheet(ExcelPackage pkg)
    {
        var aliasLookup = new Dictionary<string, string>();
        foreach (var (key, names) in ColumnAliases)
            foreach (var n in names) aliasLookup.TryAdd(Norm(n), key);

        foreach (var ws in pkg.Workbook.Worksheets)
        {
            if (ws.Dimension == null) continue;
            int maxCol = Math.Min(ws.Dimension.End.Column, 120);
            for (int r = 1; r <= Math.Min(ws.Dimension.End.Row, 15); r++)
            {
                var cols = new Dictionary<string, int>();
                for (int c = 1; c <= maxCol; c++)
                {
                    var h = Norm(ws.Cells[r, c].Text);
                    if (h != "" && aliasLookup.TryGetValue(h, out var key)) cols.TryAdd(key, c);
                }
                if (cols.ContainsKey("code") && cols.ContainsKey("name")) return (ws, r, cols);
            }
        }
        return (null, 0, new());
    }

    // ── Normalization helpers ─────────────────────────────────────────────
    public static string Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Replace("ـ", "").Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ة', 'ه').Replace('ى', 'ي');
        return Regex.Replace(t, @"\s+", "").ToLowerInvariant();
    }

    // A trailing number on a COMPANY name is a data-entry typo ("...التجارية2"); on a branch it's meaningful.
    private static string CompanyKey(string s) => StripTrailingDigits(Norm(s));
    // Branch names: ignore spaces/hamza/brackets/dashes and a leading "فرع" ("فرع العليا مول" = "العليا مول").
    private static string BranchKey(string s)
    {
        var k = Regex.Replace(Norm(s), @"[()\[\]\-_.،,]", "");
        return k.StartsWith("فرع") && k.Length > 5 ? k[3..] : k;
    }

    private static string StripTrailingDigits(string s) => Regex.Replace(s, @"[0-9٠-٩]+$", "").Trim();

    private static string CleanSpaces(string s) => Regex.Replace(s.Replace("ـ", ""), @"\s+", " ").Trim();

    private static string Str(object? v) => v switch
    {
        null => "",
        double d => d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
        _ => v.ToString()?.Trim() ?? "",
    };

    private static string Get(Row r, string key) => r.Cells.TryGetValue(key, out var v) ? Str(v) : "";

    private static decimal? Dec(Row r, string key)
    {
        if (!r.Cells.TryGetValue(key, out var v) || v == null) return null;
        if (v is double d) return (decimal)d;
        return decimal.TryParse(Str(v).Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : null;
    }

    private static int? Int(Row r, string key) { var d = Dec(r, key); return d.HasValue ? (int)Math.Round(d.Value) : null; }

    private static readonly UmAlQuraCalendar Hijri = new();

    // Accepts Excel dates, "yyyy/MM/dd", "yyyy-MM-dd[ HH:mm:ss]", "dd/MM/yyyy"; years < 1600 are treated as Hijri (Umm al-Qura).
    public static DateOnly? ParseDate(object? v, out bool wasHijri)
    {
        wasHijri = false;
        if (v == null) return null;
        if (v is DateTime dt) return DateOnly.FromDateTime(dt);
        if (v is double od) { try { return DateOnly.FromDateTime(DateTime.FromOADate(od)); } catch { return null; } }
        var s = Str(v).Split(' ')[0].Replace('-', '/').Replace('.', '/');
        var p = s.Split('/');
        if (p.Length != 3 || !p.All(x => int.TryParse(x, out _))) return null;
        int a = int.Parse(p[0]), b = int.Parse(p[1]), c = int.Parse(p[2]);
        int y, m, d;
        if (a > 31) { y = a; m = b; d = c; } else { y = c; m = b; d = a; }
        try
        {
            if (y >= 1300 && y <= 1500)
            {
                wasHijri = true;
                d = Math.Min(d, Hijri.GetDaysInMonth(y, m));
                return DateOnly.FromDateTime(Hijri.ToDateTime(y, m, d, 0, 0, 0, 0));
            }
            if (y >= 1900 && y <= 2100) return new DateOnly(y, m, Math.Min(d, DateTime.DaysInMonth(y, m)));
        }
        catch (ArgumentException) { }
        return null;
    }

    private static string? NormalizePhone(string raw, out bool changed)
    {
        changed = false;
        var d = Regex.Replace(raw, @"\D", "");
        if (d == "") return null;
        string? result = null;
        if (d.StartsWith("00966")) d = d[5..];
        else if (d.StartsWith("966")) d = d[3..];
        if (d.Length == 9 && d.StartsWith("5")) result = "0" + d;
        else if (d.Length == 10 && d.StartsWith("05")) result = d;
        else if (d.Length == 9 && d.StartsWith("1")) result = "0" + d;   // landline without leading 0
        else if (d.Length == 10 && d.StartsWith("01")) result = d;
        changed = result != null && result != raw.Trim();
        return result;
    }

    private static LookupItem? MapNationality(string raw)
    {
        var k = Norm(raw);
        if (k == "") return null;
        string Core(string x) { x = Norm(x); if (x.StartsWith("ال") && x.Length > 4) x = x[2..]; return x.TrimEnd('ه'); }
        if (NationalityAliases.TryGetValue(k, out var code) || NationalityAliases.TryGetValue(Core(raw), out code))
            return Lookups.Nationalities.FirstOrDefault(n => n.Code == code);
        return Lookups.Nationalities.FirstOrDefault(n => n.Code != "OTH" && Core(n.NameAr) == Core(raw));
    }

    private static string? MapBank(string raw)
    {
        var k = Norm(raw);
        if (k == "") return null;
        string? code =
            k.Contains("راجحي") ? "RJHI" :
            k.Contains("اهلي") || k.Contains("سامبا") ? "NCB" :
            k.Contains("انماء") ? "ALINMA" :
            k.Contains("العربيالوطني") || k.Contains("عربي") ? "ANB" :
            k.Contains("بريطاني") || k.Contains("ساب") || k.Contains("هولندي") || k.Contains("الاول") ? "SABB" :
            k.Contains("بلاد") ? "ALBI" :
            k.Contains("جزيره") ? "BJAZ" :
            k.Contains("فرنسي") ? "BSFR" :
            k.Contains("استثمار") ? "SAIB" :
            k.Contains("رياض") ? "RIBL" :
            k.Contains("stc") || k.Contains("استيسي") ? "STCPAY" : null;
        return code == null ? Trunc(raw, 120) : Lookups.SaudiBanks.First(b => b.Code == code).NameAr;
    }

    private static MaritalStatus? MapMarital(string raw)
    {
        var k = Norm(raw);
        if (k == "") return null;
        if (k.Contains("عزب")) return MaritalStatus.Single;
        if (k.Contains("متزوج")) return MaritalStatus.Married;
        if (k.Contains("مطلق")) return MaritalStatus.Divorced;
        if (k.Contains("ارمل")) return MaritalStatus.Widowed;
        return null;
    }

    // "عبد الله" / "أبو بكر" / "بن" stay attached to the following word so they count as one name part.
    private static (string first, string? father, string? grand, string family) SplitName(string full)
    {
        var tokens = full.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var parts = new List<string>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if ((t is "عبد" or "ابو" or "أبو" or "بن" or "بنت" or "ال") && i + 1 < tokens.Count) { parts.Add(t + " " + tokens[i + 1]); i++; }
            else parts.Add(t);
        }
        if (parts.Count == 0) return ("", null, null, "");
        if (parts.Count == 1) return (Trunc(parts[0], 60), null, null, "");
        var first = Trunc(parts[0], 60);
        var family = Trunc(parts[^1], 60);
        string? father = parts.Count >= 3 ? Trunc(parts[1], 60) : null;
        string? grand = parts.Count >= 4 ? Trunc(string.Join(" ", parts.Skip(2).Take(parts.Count - 3)), 60) : null;
        return (first, father, grand, family);
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "؟؟";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)];
        return $"{parts[0][..1]}{parts[1][..1]}";
    }

    private static string OrgInitials(string name)
    {
        var skip = new HashSet<string> { "شركة", "شركه", "مؤسسة", "مؤسسه" };
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => !skip.Contains(p)).ToArray();
        if (parts.Length == 0) return "؟";
        return string.Concat(parts.Take(2).Select(p => p[..1]));
    }

    private static string NewSlug(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 13)];

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];
}
