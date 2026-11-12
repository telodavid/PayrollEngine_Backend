namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed record UpdateMasterDataFieldRequest(
    int MasterDataGroupId,
    string Code,
    string DisplayName,
    int FieldTypeId,
    bool IsRequired,
    bool IsUnique,
    int DisplayOrder,
    bool IsActive);