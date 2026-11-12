namespace PayrollEngine.PayrollRunApp.Models;

public sealed class PayrollRunFilterSnapshot
{
    public List<int>? EmployeeIds { get; init; }
    public int? EmployeeIdFrom { get; init; }
    public int? EmployeeIdTo { get; init; }
    public int? CompanyId { get; init; }
    public bool IncludeInactive { get; init; }
}