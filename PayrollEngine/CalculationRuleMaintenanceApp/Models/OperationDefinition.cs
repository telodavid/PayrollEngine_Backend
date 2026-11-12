namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed class OperationDefinition
{
    public string Code { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Symbol { get; init; }

    public string? ResultType { get; init; }

    public List<string>? SupportedTypes { get; init; }

    public string? SourceCatalog { get; init; }

    public List<OperationInputDefinition> Inputs { get; init; } = [];
}