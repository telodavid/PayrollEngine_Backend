namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed class OperationCatalogDefinition
{
    public int CatalogVersion { get; init; }

    public List<string> ValueTypes { get; init; } = [];

    public List<OperationDefinition> Operations { get; init; } = [];
}