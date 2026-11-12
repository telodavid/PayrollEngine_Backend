namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record PayrollElementModel(
    int PayrollElementId,
    string Code,
    string Name,
    bool IsActive);