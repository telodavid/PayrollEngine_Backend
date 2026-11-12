namespace PayrollEngine.RoleCheckAndLogApp;

public sealed record UserRole(
    int RoleId,
    string Code,
    string RoleName,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record UserApp(
    int AppId,
    string Code,
    string AppName);

public sealed record UserAccess(
    int UserId,
    string UserName,
    IReadOnlyList<UserRole> Roles,
    IReadOnlyList<UserApp> Apps);
