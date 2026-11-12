namespace PayrollEngine.UserMaintenanceApp;

public sealed record AssignRoleToUserRequest(
    int RoleId,
    DateOnly ValidFrom,
    DateOnly? ValidTo);