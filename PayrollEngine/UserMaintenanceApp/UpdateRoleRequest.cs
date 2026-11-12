namespace PayrollEngine.UserMaintenanceApp;

public sealed record UpdateRoleRequest(
    string Code,
    string RoleName,
    bool IsActive);