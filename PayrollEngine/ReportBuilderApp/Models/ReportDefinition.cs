using System.Text.Json;

namespace PayrollEngine.ReportBuilderApp.Models;

public sealed class ReportDefinition
{
    public string ReportCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public string LayoutMode { get; set; } = "TABLE";

    public string DataSource { get; set; } = string.Empty;

    public ReportDateFilterDefinition DateFilter { get; set; } = new();

    public List<ReportRelationDefinition> Relations { get; set; } = [];

    public string? XmlRootElementName { get; set; }
    public string? XmlRecordElementName { get; set; }
    public string? FileNamePattern { get; set; }

    public List<ReportFieldDefinition> Fields { get; set; } = [];

    public ReportGroupingDefinition Grouping { get; set; } = new();

    public List<ReportParameterDefinition> Parameters { get; set; } = [];

    public List<ReportFilterDefinition> Filters { get; set; } = [];

    public ReportOutputDefinition? Output { get; set; }

    public ReportEmployeeDocumentDefinition? EmployeeDocument { get; set; }
}

public sealed class ReportEmployeeDocumentDefinition
{
    public string MainSectionTitle { get; set; } = "Adatok";

    public string MainWorksheetName { get; set; } = "Adatok";

    public ReportDocumentHeaderDefinition Header { get; set; } = new();

    public List<ReportDocumentSectionDefinition> AdditionalSections
    {
        get;
        set;
    } = [];
}

public sealed class ReportDocumentHeaderDefinition
{
    public bool IsEnabled { get; set; } = true;

    public string DataSource { get; set; } = "EMPLOYEE";

    public ReportDateFilterDefinition DateFilter { get; set; } = new()
    {
        Mode = "NONE"
    };

    public List<ReportRelationDefinition> Relations { get; set; } = [];

    public List<ReportFieldDefinition> Fields { get; set; } = [];

    public List<ReportParameterDefinition> Parameters { get; set; } = [];

    public List<ReportFilterDefinition> Filters { get; set; } = [];
}

public sealed class ReportDocumentSectionDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
    public bool StartNewPage { get; set; } = true;

    public string DataSource { get; set; } = string.Empty;

    public ReportDateFilterDefinition DateFilter { get; set; } = new()
    {
        Mode = "NONE"
    };

    public List<ReportRelationDefinition> Relations { get; set; } = [];

    public List<ReportFieldDefinition> Fields { get; set; } = [];

    public ReportGroupingDefinition Grouping { get; set; } = new();

    public List<ReportParameterDefinition> Parameters { get; set; } = [];

    public List<ReportFilterDefinition> Filters { get; set; } = [];
}

public sealed class ReportDateFilterDefinition
{
    public string Mode { get; set; } = "MONTH_RANGE";

    public string? DateField { get; set; }
    public string? ValidFromField { get; set; }
    public string? ValidToField { get; set; }
}

public sealed class ReportRelationDefinition
{
    public string RelationCode { get; set; } = string.Empty;

    public string JoinType { get; set; } = "LEFT";

    public ReportDateFilterDefinition DateFilter { get; set; } = new()
    {
        Mode = "NONE"
    };
}

public sealed class ReportFieldDefinition
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OutputName { get; set; } = string.Empty;

    public ReportValueDefinition Value { get; set; } = new();

    public string Aggregation { get; set; } = "NONE";

    public string? Format { get; set; }
}

public sealed class ReportValueDefinition
{
    public string Kind { get; set; } = "REFERENCE";

    public string? Reference { get; set; }

    public JsonElement? Constant { get; set; }
}

public sealed class ReportGroupingDefinition
{
    public string RowMode { get; set; } = "DETAIL";

    public List<string> GroupBy { get; set; } = [];

    public List<string> SplitFilesBy { get; set; } = [];

    public List<string> SortBy { get; set; } = [];
}

public sealed class ReportParameterDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";

    public bool Required { get; set; }

    public JsonElement? DefaultValue { get; set; }

    public string? Mode { get; set; }
}

public sealed class ReportFilterDefinition
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "EQUALS";

    public string? Parameter { get; set; }

    public JsonElement? Constant { get; set; }
}

public sealed class ReportOutputDefinition
{
    public string? Format { get; set; }

    public ReportXmlDefinition? Xml { get; set; }
}

public sealed class ReportXmlDefinition
{
    public string? Namespace { get; set; }

    public ReportXmlNodeDefinition? Root { get; set; }
}

public sealed class ReportXmlNodeDefinition
{
    public string Name { get; set; } = string.Empty;

    public string? RepeatFor { get; set; }
    public string? ValueField { get; set; }

    public JsonElement? Constant { get; set; }

    public List<ReportXmlNodeDefinition> Children { get; set; } = [];
}