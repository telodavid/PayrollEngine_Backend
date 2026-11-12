namespace PayrollEngine.PayrollRunApp.Models;

public sealed class PayrollRunElementLog
{
    public int? PayrollElementId { get; init; }
    public string PayrollElementCode { get; init; } = string.Empty;
    public string PayrollElementName { get; init; } = string.Empty;
    public string ResultType { get; init; } = string.Empty;

    public string? RuleCode { get; init; }

    public decimal Amount { get; init; }

    public decimal? PreviousActualAmount { get; init; }
    public decimal? DifferenceFromPreviousActual { get; init; }
}