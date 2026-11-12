namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record PayrollElementFieldValueModel(
    int PayrollElementId,
    int FieldId,
    string? FieldValue,
    int? OptionId,
    DateOnly ValidFrom,
    DateOnly? ValidTo);