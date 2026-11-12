namespace PayrollEngine.ReportBuilderApp.Models;

public sealed class ReportDataCatalogDefinition
{
    public int Version { get; set; } = 1;

    public List<ReportDataSourceDefinition> Sources { get; set; } = [];
}

public sealed class ReportDataSourceDefinition
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public ReportDataEntityDefinition Root { get; set; } = new();

    public string? EmployeeIdField { get; set; }
    public string? CompanyIdField { get; set; }

    public ReportDataValidityDefinition? Validity { get; set; }

    public List<ReportDataFieldDefinition> Fields { get; set; } = [];

    public List<ReportDataRelationDefinition> Relations { get; set; } = [];

    public List<ReportParameterDefinition> Parameters { get; set; } = [];

    public List<ReportFilterDefinition> Filters { get; set; } = [];
}

public sealed class ReportDataEntityDefinition
{
    public string Schema { get; set; } = "dbo";
    public string Entity { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
}

public sealed class ReportDataFieldDefinition
{
    public string Reference { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string EntityAlias { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;

    public string Type { get; set; } = "text";

    public bool IsNullable { get; set; }

    public bool CanFilter { get; set; } = true;
    public bool CanGroup { get; set; } = true;
    public bool CanSort { get; set; } = true;

    public string? RequiredRelationCode { get; set; }

    public ReportDynamicFieldDefinition? DynamicField { get; set; }
}

public sealed class ReportDataRelationDefinition
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public ReportDataEntityDefinition Target { get; set; } = new();

    public string JoinType { get; set; } = "LEFT";
    public string Cardinality { get; set; } = "ONE_TO_MANY";

    public string? ParentRelationCode { get; set; }

    public List<ReportDataJoinConditionDefinition> Conditions { get; set; } = [];

    public ReportDataValidityDefinition? Validity { get; set; }
}

public sealed class ReportDataJoinConditionDefinition
{
    public string LeftEntityAlias { get; set; } = string.Empty;
    public string LeftColumn { get; set; } = string.Empty;

    public string RightEntityAlias { get; set; } = string.Empty;
    public string RightColumn { get; set; } = string.Empty;
}

public sealed class ReportDataValidityDefinition
{
    public string? DateField { get; set; }
    public string? ValidFromField { get; set; }
    public string? ValidToField { get; set; }
}

public sealed class ReportDynamicFieldDefinition
{
    public int MasterDataFieldId { get; set; }

    public string OwnerEntityAlias { get; set; } = string.Empty;
    public string OwnerIdColumn { get; set; } = string.Empty;

    public ReportDataEntityDefinition ValueEntity { get; set; } = new();

    public string OwnerForeignKeyColumn { get; set; } = string.Empty;
    public string FieldIdColumn { get; set; } = "MasterDataFieldId";
    public string ValueColumn { get; set; } = string.Empty;

    public string? ValidFromColumn { get; set; }
    public string? ValidToColumn { get; set; }
}