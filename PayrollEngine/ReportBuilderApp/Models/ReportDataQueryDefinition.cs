using System.Text.Json;

namespace PayrollEngine.ReportBuilderApp.Models;

public sealed class ReportDataQueryDefinition
{
    public ReportDataEntityDefinition Root { get; set; } = new();

    public DateOnly? DynamicFieldAsOfDate { get; set; }

    public List<ReportDataQueryField> Fields { get; set; } = [];

    public List<ReportDataQueryJoin> Joins { get; set; } = [];

    public List<ReportDataQueryFilter> Filters { get; set; } = [];

    public List<ReportDataQuerySort> Sorts { get; set; } = [];

    public ReportDataQueryLatestRecords? LatestRecords { get; set; }
}

public sealed class ReportDataQueryField
{
    public string Reference { get; set; } = string.Empty;

    public string EntityAlias { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;

    public string DataType { get; set; } = "text";

    public ReportDynamicFieldDefinition? DynamicField { get; set; }
}

public sealed class ReportDataQueryJoin
{
    public string Code { get; set; } = string.Empty;

    public ReportDataEntityDefinition Target { get; set; } = new();

    public string JoinType { get; set; } = "LEFT";

    public List<ReportDataJoinConditionDefinition> Conditions
    {
        get;
        set;
    } = [];

    public List<ReportDataQueryFilter> Filters { get; set; } = [];
}

public sealed class ReportDataQueryFilter
{
    public string EntityAlias { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;

    public string DataType { get; set; } = "text";
    public string Operator { get; set; } = "EQUALS";

    public JsonElement? Value { get; set; }

    public bool IncludeNull { get; set; }

    public ReportDynamicFieldDefinition? DynamicField { get; set; }
}

public sealed class ReportDataQuerySort
{
    public string Reference { get; set; } = string.Empty;

    public string Direction { get; set; } = "ASC";
}

public sealed class ReportDataQueryLatestRecords
{
    public List<string> PartitionColumns { get; set; } = [];

    public List<ReportDataQueryRecordOrder> OrderBy { get; set; } = [];
}

public sealed class ReportDataQueryRecordOrder
{
    public string Column { get; set; } = string.Empty;

    public string Direction { get; set; } = "DESC";
}