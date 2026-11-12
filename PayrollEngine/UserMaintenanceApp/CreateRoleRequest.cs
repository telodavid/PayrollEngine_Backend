namespace PayrollEngine.UserMaintenanceApp;

public sealed record CreateRoleRequest(
    string Code,
    string RoleName);