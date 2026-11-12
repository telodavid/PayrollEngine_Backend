namespace PayrollEngine.PayrollSchemaMaintenanceApp.Models;

public sealed class PayrollSchemaBlockDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<PayrollSchemaRuleReference> Rules { get; set; } = [];
}