namespace PayrollEngine.PayrollRunApp.Models;

public sealed class PayrollRunLog
{
    public Guid RunId { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? FinishedAtUtc { get; set; }

    public int? UserId { get; init; }
    public string? UserName { get; init; }

    public string SchemaCode { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }

    public string PeriodFrom { get; init; } = string.Empty;
    public string PeriodTo { get; init; } = string.Empty;

    public bool IsSimulation { get; init; }
    public string Status { get; set; } = "Running";
    public PayrollRunFilterSnapshot Filters { get; init; } = new();
    public List<PayrollRunEmployeeLog> Employees { get; init; } = [];
}