namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record EmployeeFieldGroupModel(
    int MasterDataGroupId,
    string Code,
    string Name,
    IReadOnlyList<EmployeeFieldModel> Fields);

public sealed record EmployeeFieldModel(
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
    IReadOnlyList<EmployeeFieldOptionModel> Options);

public sealed record EmployeeFieldOptionModel(
    int FieldId,
    int OptionId,
    string OptionCode,
    string DisplayName,
    int SortOrder);
public sealed record SaveEmployeeFieldValueRequest(
    DateOnly ValidFrom,
    string? FieldValue,
    int? OptionId,
    DateOnly? RetroPayrollRunFrom);
public sealed record EmployeeFieldValueSaveResult(
    int EmployeeId,
    int FieldId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool RequiresRetroPayroll,
    DateOnly? SuggestedPayrollRunFrom);
public sealed record EndEmployeeFieldValueRequest(
    DateOnly ValidFrom,
    DateOnly ValidTo,
    DateOnly? RetroPayrollRunFrom);

public sealed record EmployeeFieldValueEndResult(
    int EmployeeId,
    int FieldId,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    bool RequiresRetroPayroll,
    DateOnly? SuggestedPayrollRunFrom);