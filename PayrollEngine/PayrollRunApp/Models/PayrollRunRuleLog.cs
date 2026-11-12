namespace PayrollEngine.PayrollRunApp.Models;

public sealed class PayrollRunRuleLog
{
    public string RuleCode { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;

    public string Status { get; set; } = "Succeeded";
    public string? ErrorMessage { get; set; }

    public Dictionary<string, string?> Variables { get; init; } = [];

    public List<PayrollRunVariableChangeLog> VariableChanges { get; init; } = [];

    public List<PayrollRunOperationLog> Operations { get; init; } = [];
}

public sealed record PayrollRunVariableChangeLog(
    int SequenceNumber,
    string Name,
    object? PreviousValue,
    object? NewValue);

public sealed record PayrollRunOperationLog(
    int SequenceNumber,
    string OperationCode,
    string Status,
    string? Result,
    string? Description,
    string? ExecutionContext);