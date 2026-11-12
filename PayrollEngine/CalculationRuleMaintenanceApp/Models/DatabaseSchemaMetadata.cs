namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed record DatabaseSchemaMetadata(
    IReadOnlyList<DatabaseColumnMetadata> Columns,
    IReadOnlyList<DatabaseRelationshipMetadata> Relationships);