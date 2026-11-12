using System.Text.Json;

namespace PayrollEngine.CalculationRuleMaintenanceApp.Models;

public sealed class CalculationRuleDefinition
{
    public string RuleCode { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public bool IsActive { get; init; } = true;

    public string RuleType { get; init; } = "normal";

    public List<CalculationRuleParameter> Parameters { get; init; } = [];

    public string? ReturnType { get; init; }

    public JsonElement? ReturnExpression { get; init; }

    public List<JsonElement> Steps { get; init; } = [];
}

public sealed class CalculationRuleParameter
{
    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;
}