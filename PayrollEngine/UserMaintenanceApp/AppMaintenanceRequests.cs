namespace PayrollEngine.AppMaintenanceApp;

public sealed record CreateAppRequest(
    string Code,
    string AppName);

public sealed record UpdateAppRequest(
    string Code,
    string AppName,
    bool IsActive);