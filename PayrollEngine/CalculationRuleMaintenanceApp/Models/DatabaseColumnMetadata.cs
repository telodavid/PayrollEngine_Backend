namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed record DatabaseColumnMetadata(
    string SchemaName,
    string TableName,
    int ColumnOrder,
    string ColumnName,
    string DataType,
    int MaxLength,
    byte NumericPrecision,
    byte NumericScale,
    bool IsNullable,
    bool IsIdentity,
    bool IsPrimaryKey,
    bool IsForeignKey);