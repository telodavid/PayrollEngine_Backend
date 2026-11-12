namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record SavePayrollElementFieldValueRequest(
    int FieldId,
    string? FieldValue,
    int? OptionId,
    DateOnly ValidFrom,
    DateOnly? ValidTo);