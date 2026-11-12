namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record PayrollElementFieldModel(
    int FieldId,
    string Code,
    string DisplayName,
    int FieldTypeId,
    string FieldTypeCode,
    string FieldTypeName,
    bool IsRequired,
    bool IsUnique,
    int DisplayOrder,
    bool CanRead,
    bool CanEdit,
    string? FieldValue,
    int? OptionId,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<PayrollElementFieldOptionModel> Options);