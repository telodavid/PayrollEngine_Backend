namespace PayrollEngine.UserMaintenanceApp;

public sealed record MdgRolePermissionSummary(
    int RoleId,
    int MasterDataGroupId,
    string Code,
    string Name,
    bool CanRead,
    bool CanEdit);