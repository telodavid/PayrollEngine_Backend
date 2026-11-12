namespace PayrollEngine.UserMaintenanceApp;

public sealed record AppSummary(
    int AppId,
    string Code,
    string AppName,
    bool IsActive);