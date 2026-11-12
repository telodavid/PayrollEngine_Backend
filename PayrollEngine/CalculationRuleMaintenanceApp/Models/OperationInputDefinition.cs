namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed class OperationInputDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public bool Required { get; init; }

    public string? SameTypeAs { get; init; }

    public int? MinimumItems { get; init; }

    public bool? CannotBeZero { get; init; }

    public string? SourceCatalog { get; init; }
}