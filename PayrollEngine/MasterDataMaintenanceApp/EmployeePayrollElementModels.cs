namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record SetEmployeePayrollElementRequest(
    int EmploymentId,
    int PayrollElementId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    decimal Amount,
    DateOnly? RetroPayrollRunFrom);

public sealed record EmployeePayrollElementChangeResult(
    bool RequiresRetroPayroll,
    DateOnly? SuggestedPayrollRunFrom,
    int? EmploymentPayrollElementId,
    int EmployeeId,
    int EmploymentId,
    int PayrollElementId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    decimal Amount,
    string Message);

public sealed record EmployeePayrollElementListItem(
    int EmployeeId,
    int EmploymentPayrollElementId,
    int EmploymentId,
    int CompanyId,
    string CompanyName,
    int PayrollElementId,
    string PayrollElementCode,
    string PayrollElementName,
    bool PayrollElementIsActive,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    decimal Amount);

public sealed record EmployeePayrollElementDeleteResult(
    int EmployeeId,
    int EmploymentPayrollElementId,
    DateOnly PayrollRunFrom);