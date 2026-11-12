namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record MasterDataGroupRoleAccess(
    int RoleId,
    string RoleCode,
    string RoleName,
    bool CanRead,
    bool CanEdit);

public sealed record MasterDataGroupAccess(
    int MasterDataGroupId,
    string Code,
    string Name,
    bool CanRead,
    bool CanEdit,
    IReadOnlyList<MasterDataGroupRoleAccess> Roles);