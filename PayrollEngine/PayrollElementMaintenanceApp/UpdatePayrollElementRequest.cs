namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record UpdatePayrollElementRequest(
    string Code,
    string Name,
    bool IsActive);