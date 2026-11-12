namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed class PayrollDataReferenceDefinition
{
    public string Reference { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string? CollectionReference { get; init; }

    public string? ValueCatalog { get; init; }

    public string? Description { get; init; }
}