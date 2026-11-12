namespace PayrollEngine.UserMaintenanceApp;

public sealed record SetMdgRolePermissionsRequest(
    int MasterDataGroupId,
    bool CanRead,
    bool CanEdit);