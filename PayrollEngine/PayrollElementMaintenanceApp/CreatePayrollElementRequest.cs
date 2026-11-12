namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record CreatePayrollElementRequest(
    string Code,
    string Name);