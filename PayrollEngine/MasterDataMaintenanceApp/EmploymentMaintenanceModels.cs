namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record ChangeEmploymentCompanyRequest(
    int CompanyId,
    DateOnly ValidFrom,
    DateOnly? RetroPayrollRunFrom);

public sealed record EmploymentChangeResult(
    bool RequiresRetroPayroll,
    DateOnly? SuggestedPayrollRunFrom,
    int? EmployeeId,
    int? EmploymentId,
    int? CompanyId,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string Message);
public sealed record EmploymentHistoryItem(
    int EmployeeId,
    int EmploymentId,
    int CompanyId,
    string CompanyName,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record EmploymentDeleteResult(
    int EmployeeId,
    int EmploymentId,
    DateOnly? PayrollRunFrom);