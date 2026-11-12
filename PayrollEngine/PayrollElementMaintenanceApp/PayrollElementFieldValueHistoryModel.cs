namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record PayrollElementFieldValueHistoryModel(
    string? FieldValue,
    int? OptionId,
    string? OptionDisplayName,
    DateOnly ValidFrom,
    DateOnly? ValidTo);