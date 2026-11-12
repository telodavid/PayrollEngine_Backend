namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed record DynamicFieldMetadata(
    int MasterDataGroupId,
    string MasterDataGroupCode,
    string MasterDataGroupName,
    int FieldId,
    string FieldCode,
    string DisplayName,
    int FieldTypeId,
    string FieldTypeCode,
    string FieldTypeName,
    bool IsRequired,
    bool IsUnique,
    int DisplayOrder,
    bool IsActive);