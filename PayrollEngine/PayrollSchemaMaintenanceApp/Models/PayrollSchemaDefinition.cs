namespace PayrollEngine.PayrollSchemaMaintenanceApp.Models;

public sealed class PayrollSchemaDefinition
{
    public string SchemaCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public List<PayrollSchemaBlockDefinition> Blocks { get; set; } = [];
}