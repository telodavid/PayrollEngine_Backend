namespace PayrollEngine.ReportBuilderApp.Models;

public sealed class ReportEmployeeDocumentData
{
    public string ReportCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int EmployeeId { get; set; }

    public List<ReportDocumentHeaderValue> Header { get; set; } = [];

    public List<ReportDocumentSectionData> Sections { get; set; } = [];

    public Dictionary<string, object?> FileNameValues { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportDocumentHeaderValue
{
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string OutputName { get; set; } = string.Empty;

    public object? Value { get; set; }

    public string? Format { get; set; }
}

public sealed class ReportDocumentSectionData
{
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string WorksheetName { get; set; } = string.Empty;

    public bool StartNewPage { get; set; } = true;

    public IReadOnlyList<ReportFieldDefinition> Fields { get; set; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> OutputRows
    {
        get;
        set;
    } = [];
}