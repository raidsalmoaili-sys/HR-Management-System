namespace Project_HR_1.Services.Scope;

public interface IScopeContext
{
    HrScope Current { get; }
    string Color { get; }
    string LabelAr { get; }
    string LabelEn { get; }
    IQueryable<T> Filter<T>(IQueryable<T> source) where T : IScoped;
}
