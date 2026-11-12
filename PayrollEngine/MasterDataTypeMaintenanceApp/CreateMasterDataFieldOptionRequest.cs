namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed record CreateMasterDataFieldOptionRequest(
    string OptionCode,
    string DisplayName,
    int SortOrder);