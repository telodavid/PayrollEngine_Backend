namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed record MasterDataFieldModel(
    int FieldId,
    int MasterDataGroupId,
    string MasterDataGroupCode,
    string MasterDataGroupName,
    string Code,
    string DisplayName,
    int FieldTypeId,
    string FieldTypeCode,
    string FieldTypeName,
    bool IsRequired,
    bool IsUnique,
    int DisplayOrder,
    bool IsActive);