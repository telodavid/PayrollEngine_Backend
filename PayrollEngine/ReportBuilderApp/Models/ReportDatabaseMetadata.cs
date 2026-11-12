namespace PayrollEngine.ReportBuilderApp.Models;

public sealed class ReportDatabaseMetadata
{
    public List<ReportDatabaseColumn> Columns { get; set; } = [];

    public List<ReportDatabaseForeignKeyColumn> ForeignKeyColumns
    {
        get;
        set;
    } = [];
}

public sealed class ReportDatabaseColumn
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string TableDisplayName { get; set; } = string.Empty;

    public int ColumnOrder { get; set; }

    public string ColumnName { get; set; } = string.Empty;
    public string SqlType { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;

    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }

    public int? PrimaryKeyOrder { get; set; }

    public short MaxLengthBytes { get; set; }

    public byte NumericPrecision { get; set; }
    public byte NumericScale { get; set; }
}

public sealed class ReportDatabaseForeignKeyColumn
{
    public int ForeignKeyId { get; set; }

    public string ForeignKeyName { get; set; } = string.Empty;

    public int ColumnOrder { get; set; }

    public string ParentSchemaName { get; set; } = string.Empty;
    public string ParentTableName { get; set; } = string.Empty;
    public string ParentColumnName { get; set; } = string.Empty;

    public string ReferencedSchemaName { get; set; } = string.Empty;
    public string ReferencedTableName { get; set; } = string.Empty;
    public string ReferencedColumnName { get; set; } = string.Empty;

    public bool IsNullable { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsNotTrusted { get; set; }
}