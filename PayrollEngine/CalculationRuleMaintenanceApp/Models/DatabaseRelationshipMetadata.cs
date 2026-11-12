namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed record DatabaseRelationshipMetadata(
    string ForeignKeyName,
    string SourceSchema,
    string SourceTable,
    string SourceColumn,
    string TargetSchema,
    string TargetTable,
    string TargetColumn);