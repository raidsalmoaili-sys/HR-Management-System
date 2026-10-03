using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using Project_HR_1.Models;
using Project_HR_1.Models.Hr;
using Project_HR_1.Services.Audit;
using Project_HR_1.Services.Documents;
using Project_HR_1.Services.I18n;
using Project_HR_1.Services.Permissions;
using Project_HR_1.Services.Scope;
using QuestPDF.Infrastructure;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Local, git-ignored overrides (real connection string / passwords). See README.md.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews()
    // Large TempData payloads (e.g. bulk-import error results) were being crammed into a
    // cookie by the default CookieTempDataProvider, which blows past browser response-header
    // size limits (ERR_RESPONSE_HEADERS_TOO_BIG). Store TempData server-side instead.
    .AddSessionStateTempDataProvider();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

// HR services
builder.Services.AddScoped<IHrLocalizer, HrLocalizer>();
builder.Services.AddScoped<IScopeContext, ScopeContext>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IActivityLogger, ActivityLogger>();
builder.Services.AddScoped<IContractPdfBuilder, ContractPdfBuilder>();
builder.Services.AddScoped<IRosterPdfBuilder, RosterPdfBuilder>();
builder.Services.AddScoped<Project_HR_1.Services.HrModules.PayrollCalculator>();

var app = builder.Build();

// ─── Manual seed CLI: `dotnet run -- seed` ─────────────────────────────
if (args.Contains("seed"))
{
    if (app.Environment.IsProduction() && !args.Contains("--force"))
    {
        Console.WriteLine("[seed] Refusing to run the destructive seed in Production. Pass --force to override.");
        return;
    }

    using var seedScope = app.Services.CreateScope();
    var sp = seedScope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await HrSeeder.SeedAsync(sp);
    Console.WriteLine("[seed] complete.");
    return;
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ScopeCookieMiddleware>();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "service",
    pattern: "Service/{id}",
    defaults: new { controller = "Service", action = "Show" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
