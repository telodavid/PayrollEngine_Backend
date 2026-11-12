namespace PayrollEngine.UserMaintenanceApp;

public sealed record RoleSummary(
    int RoleId,
    string Code,
    string RoleName,
    bool IsActive);