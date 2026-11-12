namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed record CreateMasterDataFieldRequest(
    int MasterDataGroupId,
    string Code,
    string DisplayName,
    int FieldTypeId,
    bool IsRequired,
    bool IsUnique,
    int DisplayOrder);
