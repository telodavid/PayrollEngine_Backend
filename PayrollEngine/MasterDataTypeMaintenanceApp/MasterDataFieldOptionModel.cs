namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed record MasterDataFieldOptionModel(
    int OptionId,
    int FieldId,
    string OptionCode,
    string DisplayName,
    int SortOrder,
    bool IsActive);