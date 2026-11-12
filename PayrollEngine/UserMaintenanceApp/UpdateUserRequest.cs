namespace PayrollEngine.UserMaintenanceApp;

public sealed record UpdateUserRequest(
    string UserName,
    bool IsActive);