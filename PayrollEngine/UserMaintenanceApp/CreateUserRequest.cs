namespace PayrollEngine.UserMaintenanceApp;

public sealed record CreateUserRequest(
    string UserName,
    string Password);