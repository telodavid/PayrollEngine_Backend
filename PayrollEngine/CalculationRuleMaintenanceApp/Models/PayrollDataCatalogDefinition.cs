namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed class PayrollDataCatalogDefinition
{
    public int CatalogVersion { get; init; } = 1;

    public List<PayrollDataReferenceDefinition> References { get; init; } = [];
}