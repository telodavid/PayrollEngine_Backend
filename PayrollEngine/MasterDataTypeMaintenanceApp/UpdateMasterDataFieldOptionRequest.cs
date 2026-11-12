namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed record UpdateMasterDataFieldOptionRequest(
    string OptionCode,
    string DisplayName,
    int SortOrder,
    bool IsActive);