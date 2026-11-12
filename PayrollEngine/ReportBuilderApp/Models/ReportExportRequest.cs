using System.Text.Json;

namespace PayrollEngine.ReportBuilderApp.Models;

public sealed class ReportExportRequest
{
    public string Format { get; set; } = string.Empty;
    public string PeriodFrom { get; set; } = string.Empty;

    public string PeriodTo { get; set; } = string.Empty;

    public DateOnly? AsOfDate { get; set; }

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public List<int>? EmployeeIds { get; set; }

    public int? CompanyId { get; set; }

    public Dictionary<string, JsonElement>? ParameterValues { get; set; }
}