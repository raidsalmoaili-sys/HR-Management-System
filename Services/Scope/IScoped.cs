namespace Project_HR_1.Services.Scope;

// Marker interface. Per-service entities opt in so IScopeContext.Filter<T>() can hide rows by scope.
// Foundation entities do not implement this.
public interface IScoped
{
    int? CompanyId { get; }
    int? BranchId { get; }
}
