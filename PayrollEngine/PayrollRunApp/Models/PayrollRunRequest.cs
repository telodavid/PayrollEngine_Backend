namespace PayrollEngine.PayrollRunApp.Models;

public sealed class PayrollRunRequest
{
    public string SchemaCode { get; init; } = string.Empty;

    public string PeriodFrom { get; init; } = string.Empty;
    public string PeriodTo { get; init; } = string.Empty;
    public List<int>? EmployeeIds { get; init; }
    public int? EmployeeIdFrom { get; init; }
    public int? EmployeeIdTo { get; init; }
    public int? CompanyId { get; init; }
    public bool IncludeInactive { get; init; }
    public bool IsSimulation { get; init; } = true;
}