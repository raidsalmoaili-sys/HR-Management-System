using Microsoft.AspNetCore.Http;

namespace Project_HR_1.Services.I18n;

public class HrLocalizer : IHrLocalizer
{
    public const string CookieName = "hr_lang";

    private readonly string _lang;

    public HrLocalizer(IHttpContextAccessor httpContextAccessor)
    {
        var ctx = httpContextAccessor.HttpContext;
        var cookieLang = ctx?.Request.Cookies[CookieName];
        _lang = cookieLang == "en" ? "en" : "ar";
    }

    public string Lang => _lang;
    public bool IsRtl => _lang == "ar";

    public string T(string key)
    {
        if (HrDict.Map.TryGetValue(key, out var pair))
            return _lang == "en" ? pair.En : pair.Ar;
        return key;
    }

    public string L(string ar, string en)
    {
        if (_lang == "en") return string.IsNullOrEmpty(en) ? ar : en;
        return string.IsNullOrEmpty(ar) ? en : ar;
    }
}
