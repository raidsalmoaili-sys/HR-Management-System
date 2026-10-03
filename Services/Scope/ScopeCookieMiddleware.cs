using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Project_HR_1.Services.Scope;

public class ScopeCookieMiddleware
{
    public const string CookieName = "hr_scope";
    private const string ProtectorPurpose = "Project_HR_1.HrScopeCookie";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    private readonly RequestDelegate _next;
    public ScopeCookieMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var scope = ReadFromCookie(ctx) ?? HrScope.Global();
        ctx.Items[ScopeContext.ItemKey] = scope;
        await _next(ctx);
    }

    private static IDataProtector GetProtector(HttpContext ctx) =>
        ctx.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(ProtectorPurpose);

    public static HrScope? ReadFromCookie(HttpContext ctx)
    {
        var raw = ctx.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(raw)) return null;
        try
        {
            var unprotected = GetProtector(ctx).Unprotect(raw);
            return JsonSerializer.Deserialize<HrScope>(unprotected, JsonOpts);
        }
        catch { return null; }
    }

    public static void Write(HttpContext ctx, HrScope scope)
    {
        var raw = JsonSerializer.Serialize(scope, JsonOpts);
        var protected_ = GetProtector(ctx).Protect(raw);
        ctx.Response.Cookies.Append(CookieName, protected_, new CookieOptions
        {
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
        });
        ctx.Items[ScopeContext.ItemKey] = scope;
    }

    public static void Clear(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(CookieName);
        ctx.Items[ScopeContext.ItemKey] = HrScope.Global();
    }
}
