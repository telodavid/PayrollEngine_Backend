using PayrollEngine.CalculationRuleMaintenanceApp;
using PayrollEngine.CalculationRuleMaintenanceApp.Execution;
using PayrollEngine.PayrollSchemaMaintenanceApp;
using PayrollEngine.PayrollSchemaMaintenanceApp.Models;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollSchemaRuleExecutor(
    PayrollSchemaFileStorageService schemas,
    RuleFileStorageService rules,
    OperationEvaluator evaluator)
{
    public async Task<RuleExecutionContext> ExecuteAsync(
        string schemaCode,
        IReadOnlyDictionary<string, object?> data,
        CancellationToken cancellationToken = default,
        int? employeeId = null,
        DateOnly? periodStart = null,
        IReadOnlyList<PayrollResultSnapshot>? previousResults = null)
    {
        if (string.IsNullOrWhiteSpace(schemaCode))
        {
            throw new ArgumentException(
                "A sémakód megadása kötelező.",
                nameof(schemaCode));
        }

        var schema = await schemas.GetByCodeAsync(
            schemaCode,
            cancellationToken);

        if (schema is null)
        {
            throw new FileNotFoundException(
                $"A séma nem található: {schemaCode}");
        }

        if (!schema.IsActive)
        {
            throw new InvalidOperationException(
                $"A séma inaktív, ezért nem futtatható: {schemaCode}");
        }

        var availableRules = await rules.GetAllAsync(cancellationToken);
        var context = new RuleExecutionContext(
            data,
            employeeId,
            periodStart,
            previousResults,
            availableRules);

        context.PrepareWorkCalendar();

        foreach (var block in schema.Blocks ?? [])
        {
            foreach (var ruleReference in block.Rules ?? [])
            {
                var rule = availableRules.FirstOrDefault(existing =>
                    string.Equals(
                        existing.RuleCode,
                        ruleReference.RuleCode,
                        StringComparison.OrdinalIgnoreCase));

                if (rule is null)
                {
                    throw new FileNotFoundException(
                        $"A sémában hivatkozott szabály nem található: " +
                        $"{ruleReference.RuleCode}");
                }

                if (!rule.IsActive)
                {
                    throw new InvalidOperationException(
                        $"A sémában hivatkozott szabály inaktív: " +
                        $"{rule.RuleCode}");
                }

                context.CurrentRuleCode = rule.RuleCode;

                if (string.Equals(
                        rule.RuleType,
                        "callable",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"A segédszabály önállóan nem futtatható sémából: " +
                        $"{rule.RuleCode}");
                }

                foreach (var step in rule.Steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        evaluator.ExecuteStatement(step, context);
                    }
                    catch (MissingPayrollValueException)
                    {
                        var operationCode = step.TryGetProperty("code", out var code)
                            ? code.GetString() ?? "ismeretlen"
                            : "ismeretlen";

                        context.RecordOperation(
                            operationCode,
                            "SkippedMissingValue",
                            null);
                    }
                }
            }
        }

        context.CurrentRuleCode = null;
        return context;
    }
}
