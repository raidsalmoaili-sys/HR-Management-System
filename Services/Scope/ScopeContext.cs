using Microsoft.AspNetCore.Http;

namespace Project_HR_1.Services.Scope;

public class ScopeContext : IScopeContext
{
    public const string ItemKey = "HrScope";

    private readonly HrScope _scope;

    public ScopeContext(IHttpContextAccessor httpContextAccessor)
    {
        var ctx = httpContextAccessor.HttpContext;
        _scope = (ctx?.Items[ItemKey] as HrScope) ?? HrScope.Global();
    }

    public HrScope Current => _scope;

    public string Color => _scope.Level switch
    {
        ScopeLevel.Global  => "#8e44ad",
        ScopeLevel.Company => "#c8a84b",
        ScopeLevel.Branch  => "#16a085",
        _                  => "#7f8c9a",
    };

    public string LabelAr => _scope.Level switch
    {
        ScopeLevel.Global  => "كل الشركات",
        ScopeLevel.Company => "الشركة",
        ScopeLevel.Branch  => "الفرع",
        _                  => "",
    };

    public string LabelEn => _scope.Level switch
    {
        ScopeLevel.Global  => "All Companies",
        ScopeLevel.Company => "Company",
        ScopeLevel.Branch  => "Branch",
        _                  => "",
    };

    // EF Core can't translate the explicit IScoped members (e.g. Branch's "BranchId" is really its Id,
    // Company's "CompanyId" is its Id), so the filter is built against each entity's real mapped column:
    //   BranchId column  -> BranchId, or Id on Branch itself
    //   CompanyId column -> CompanyId, or Id on Company itself
    // Company-level entities (Department, JobTitle, Sponsor) have no branch column, so at Branch scope they
    // are filtered by the branch's company instead.
    public IQueryable<T> Filter<T>(IQueryable<T> source) where T : IScoped
    {
        switch (_scope.Level)
        {
            case ScopeLevel.Branch when _scope.BranchId.HasValue:
            {
                var branchCol = typeof(T) == typeof(Project_HR_1.Models.Hr.Branch) ? "Id" : HasProp<T>("BranchId") ? "BranchId" : null;
                if (branchCol != null) return source.Where(ColumnEquals<T>(branchCol, _scope.BranchId.Value));
                return FilterByCompany(source);
            }
            case ScopeLevel.Branch:
            case ScopeLevel.Company:
                return FilterByCompany(source);
            default:
                return source;
        }
    }

    private IQueryable<T> FilterByCompany<T>(IQueryable<T> source)
    {
        if (!_scope.CompanyId.HasValue) return source;
        var companyCol = typeof(T) == typeof(Project_HR_1.Models.Hr.Company) ? "Id" : HasProp<T>("CompanyId") ? "CompanyId" : null;
        return companyCol == null ? source : source.Where(ColumnEquals<T>(companyCol, _scope.CompanyId.Value));
    }

    private static bool HasProp<T>(string name) =>
        typeof(T).GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) != null;

    // x => x.<column> == value   (handles int and int? columns)
    private static System.Linq.Expressions.Expression<Func<T, bool>> ColumnEquals<T>(string column, int value)
    {
        var x = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
        var prop = System.Linq.Expressions.Expression.Property(x, column);
        var constant = System.Linq.Expressions.Expression.Constant(value, prop.Type == typeof(int?) ? typeof(int?) : typeof(int));
        var body = System.Linq.Expressions.Expression.Equal(prop, prop.Type == constant.Type ? constant : System.Linq.Expressions.Expression.Convert(constant, prop.Type));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, x);
    }
}
