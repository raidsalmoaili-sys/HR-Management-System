namespace Project_HR_1.Services.Import;

// Official company names of the (fictional) demo group seeded by HrSeeder — "مجموعة الأفق القابضة".
// Every spelling/sub-entity found in an imported employee sheet (جهة العمل / الكفيل) is folded into one of these.
// To add a new rule: add a (fragment, official name) pair — fragments are matched against the
// normalized text (no spaces, أ/إ/آ→ا, ة→ه, ى→ي), first match wins, so keep specific rules first.
// See docs/features/employee-sheet-import.md.
public static class CompanyNames
{
    public const string Trading  = "شركة الأفق للتجارة";
    public const string Medical  = "شركة الأفق للخدمات الطبية";
    public const string Contract = "مؤسسة الواحة للمقاولات";

    public static readonly IReadOnlyList<string> Official = new[] { Trading, Medical, Contract };

    private static readonly (string Fragment, string Official)[] Rules =
    {
        ("الافقللخدماتالطبيه", Medical),
        ("الطبيه",             Medical),
        ("الواحه",             Contract),
        ("مقاولات",            Contract),
        ("الافقللتجاره",       Trading),
        ("الافق",              Trading),
    };

    // A company can be split into divisions in the system (e.g. "شركة الأفق للتجارة (التجزئة)").
    // This keyword picks the right one: it must appear in the system company's name.
    private static readonly (string Fragment, string Division)[] Divisions =
    {
        ("تجزئه", "تجزئه"), ("جمله", "جمله"),
    };

    public static string? Division(string raw)
    {
        var k = EmployeeSheetImporter.Norm(raw);
        foreach (var (fragment, division) in Divisions)
            if (k.Contains(fragment)) return division;
        return null;
    }

    /// <summary>Returns the official company name for a raw sheet value, or the value itself if no rule matches.</summary>
    public static string Canonicalize(string raw)
    {
        var k = EmployeeSheetImporter.Norm(raw);
        if (k == "") return raw;
        foreach (var (fragment, official) in Rules)
            if (k.Contains(fragment)) return official;
        return raw;
    }
}
