namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record PayrollElementFieldOptionModel(
    int FieldId,
    int OptionId,
    string OptionCode,
    string DisplayName,
    int SortOrder);