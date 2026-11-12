using System.Globalization;
using System.Text.Json;
using PayrollEngine.CalculationRuleMaintenanceApp;
using PayrollEngine.CalculationRuleMaintenanceApp.Execution;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;
using PayrollEngine.PayrollRunApp.Models;
using PayrollEngine.PayrollSchemaMaintenanceApp;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollRunExecutionService(
    PayrollRunEmployeeSelectionRepository employees,
    PayrollRunDataRepository dataRepository,
    PayrollSchemaRuleExecutor schemaExecutor,
    PayrollSchemaFileStorageService schemas,
    RuleFileStorageService rules,
    PayrollRunResultRepository resultRepository,
    PayrollRunPreviousResultsRepository previousResults)
{
    public async Task<PayrollRunLog> ExecuteAsync(
        PayrollRunRequest request,
        int? userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.IsSimulation && (!userId.HasValue || userId <= 0))
        {
            throw new InvalidOperationException(
                "Éles számfejtéshez szükséges a bejelentkezett felhasználó azonosítója.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var periodFrom = ParsePeriod(
            request.PeriodFrom,
            nameof(request.PeriodFrom));
        var periodTo = ParsePeriod(
            request.PeriodTo,
            nameof(request.PeriodTo));

        if (periodFrom > periodTo)
        {
            throw new ArgumentException(
                "A kezdő periódus nem lehet későbbi a záró periódusnál.");
        }

        var schema = await schemas.GetByCodeAsync(
            request.SchemaCode,
            cancellationToken);

        if (schema is null)
        {
            throw new FileNotFoundException(
                $"A séma nem található: {request.SchemaCode}");
        }

        if (!schema.IsActive)
        {
            throw new InvalidOperationException(
                $"A séma inaktív, ezért nem futtatható: {request.SchemaCode}");
        }

        var availableRules = await rules.GetAllAsync(cancellationToken);
        var selectedEmployees = await employees.SelectAsync(
            request,
            cancellationToken);

        var runLog = new PayrollRunLog
        {
            StartedAtUtc = startedAt,
            UserId = userId,
            UserName = userName,
            SchemaCode = schema.SchemaCode,
            SchemaVersion = schema.Version,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            IsSimulation = request.IsSimulation,
            Filters = new PayrollRunFilterSnapshot
            {
                EmployeeIds = request.EmployeeIds?.ToList(),
                EmployeeIdFrom = request.EmployeeIdFrom,
                EmployeeIdTo = request.EmployeeIdTo,
                CompanyId = request.CompanyId,
                IncludeInactive = request.IncludeInactive
            }
        };

        foreach (var employee in selectedEmployees)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var employeeLog = new PayrollRunEmployeeLog
            {
                EmployeeId = employee.EmployeeId,
                EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
                JoinDate = employee.JoinDate,
                PayrollRunFromBefore = employee.PayrollRunFrom
            };

            runLog.Employees.Add(employeeLog);

            var employeeStart = FirstOfMonth(employee.EffectivePeriodFrom);
            var employeePeriodTo = periodTo;

            if (!employee.IsActive &&
                employee.TerminationDate is { } terminationDate)
            {
                var terminationMonth = FirstOfMonth(terminationDate);

                if (employeePeriodTo > terminationMonth)
                {
                    employeePeriodTo = terminationMonth;
                }
            }

            if (employeeStart > employeePeriodTo)
            {
                employeeLog.Status = "NoPeriods";
                continue;
            }

            for (var period = employeeStart;
                 period <= employeePeriodTo;
                 period = period.AddMonths(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var periodLog = new PayrollRunPeriodLog
                {
                    Period = period.ToString(
                        "yyyy-MM",
                        CultureInfo.InvariantCulture)
                };

                employeeLog.Periods.Add(periodLog);

                try
                {
                    var previousPeriodResults =
                        await previousResults.GetPreviousPeriodResultsAsync(
                            employee.EmployeeId,
                            period,
                            cancellationToken);

                    var data = await dataRepository.LoadEmployeePeriodDataAsync(
                        employee.EmployeeId,
                        period,
                        cancellationToken);

                    var context = await schemaExecutor.ExecuteAsync(
                        request.SchemaCode,
                        data,
                        cancellationToken,
                        employee.EmployeeId,
                        period,
                        previousPeriodResults
                            .Select(result => new PayrollResultSnapshot(
                                    result.PreviousRunId,
                                    result.Period,
                                    result.PayrollElementId,
                                    result.PayrollElementCode,
                                    result.PayrollElementName,
                                    result.ResultType,
                                    result.Amount))
                            .ToArray());

                    AddRuleLogs(
                        periodLog,
                        context,
                        availableRules);
                    periodLog.WorkCalendar = context.WorkCalendar.ToList();
                    periodLog.CalendarEntries = context.CalendarEntries.ToList();
                    AddElementLogs(
                        periodLog,
                        context.Results,
                        previousPeriodResults
    .Where(result => result.Period == period)
    .ToArray());

                    if (!request.IsSimulation)
                    {
                        var nextPayrollRunFrom =
                            await resultRepository.SaveEmployeePeriodResultsAsync(
                                runLog.RunId,
                                employee.EmployeeId,
                                period,
                                userId!.Value,
                                context.Results,
                                cancellationToken);

                        employeeLog.PayrollRunFromAfter =
                            nextPayrollRunFrom;
                    }

                    periodLog.Status = "Succeeded";
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    periodLog.Status = "Failed";
                    periodLog.ErrorMessage = exception.Message;
                    if (!request.IsSimulation)
                    {
                        break;
                    }
                }
            }

            var succeededCount = employeeLog.Periods.Count(
                period => period.Status == "Succeeded");
            var failedCount = employeeLog.Periods.Count(
                period => period.Status == "Failed");

            employeeLog.Status = (succeededCount, failedCount) switch
            {
                (0, 0) => "NoPeriods",
                (_, 0) => "Succeeded",
                (0, _) => "Failed",
                _ => "PartiallySucceeded"
            };
        }

        var totalSucceeded = runLog.Employees
            .SelectMany(employee => employee.Periods)
            .Count(period => period.Status == "Succeeded");

        var totalFailed = runLog.Employees
            .SelectMany(employee => employee.Periods)
            .Count(period => period.Status == "Failed");

        runLog.Status = (totalSucceeded, totalFailed) switch
        {
            (0, 0) => "NoPeriods",
            (_, 0) => "Succeeded",
            (0, _) => "Failed",
            _ => "PartiallySucceeded"
        };

        runLog.FinishedAtUtc = DateTimeOffset.UtcNow;
        return runLog;
    }

    private static void AddRuleLogs(
        PayrollRunPeriodLog periodLog,
        RuleExecutionContext context,
        IReadOnlyList<CalculationRuleDefinition> availableRules)
    {
        var ruleCodes = context.OperationTraces
            .Select(trace => trace.RuleCode)
            .Concat(context.VariableChanges.Select(change => change.RuleCode))
            .Concat(context.Results.Select(result => result.RuleCode))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var ruleCode in ruleCodes)
        {
            var rule = availableRules.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.RuleCode,
                    ruleCode,
                    StringComparison.OrdinalIgnoreCase));

            var changes = context.VariableChanges
                .Where(change => string.Equals(
                    change.RuleCode,
                    ruleCode,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var operations = context.OperationTraces
                .Where(trace => string.Equals(
                    trace.RuleCode,
                    ruleCode,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var variables = new Dictionary<string, string?>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var change in changes)
            {
                variables[change.Name] = FormatValue(change.NewValue);
            }

            periodLog.Rules.Add(new PayrollRunRuleLog
            {
                RuleCode = ruleCode!,
                RuleName = rule?.Name ?? ruleCode!,
                Variables = variables,
                VariableChanges = changes
                    .Select(change => new PayrollRunVariableChangeLog(
                        change.SequenceNumber,
                        change.Name,
                        change.PreviousValue,
                        change.NewValue))
                    .ToList(),
                Operations = operations
    .Select(operation => new PayrollRunOperationLog(
        operation.SequenceNumber,
        operation.OperationCode,
        operation.Status,
        operation.Result,
        operation.Description,
        operation.ExecutionContext))
    .ToList()
            });
        }
    }

    private static void AddElementLogs(
        PayrollRunPeriodLog periodLog,
        IReadOnlyList<CalculatedPayrollElement> currentResults,
        IReadOnlyList<PreviousPayrollElementResult> previousResults)
    {
        var previousByCode = previousResults
            .GroupBy(result => result.PayrollElementCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(result => result.ResultType switch
                    {
                        "A" => 0,
                        "O" => 1,
                        _ => 2
                    })
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var currentGroups = currentResults.GroupBy(
            result => result.PayrollElementCode,
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in currentGroups)
        {
            var currentItems = group.ToArray();
            var amount = currentItems.Sum(item => item.Amount);
            var first = currentItems[0];

            previousByCode.TryGetValue(group.Key, out var previous);

            var ruleCodes = currentItems
                .Select(item => item.RuleCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            periodLog.PayrollElements.Add(new PayrollRunElementLog
            {
                PayrollElementId = previous?.PayrollElementId,
                PayrollElementCode = first.PayrollElementCode,
                PayrollElementName =
                    previous?.PayrollElementName ?? first.PayrollElementCode,
                ResultType = "A",
                RuleCode = ruleCodes.Length == 1 ? ruleCodes[0] : null,
                Amount = amount,
                PreviousActualAmount = previous?.Amount ?? 0m,
                DifferenceFromPreviousActual =
                    amount - (previous?.Amount ?? 0m)
            });
        }
    }

    private static DateOnly ParsePeriod(string value, string parameterName)
    {
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var period))
        {
            throw new ArgumentException(
                "A periódust yyyy-MM formátumban kell megadni.",
                parameterName);
        }

        return new DateOnly(period.Year, period.Month, 1);
    }

    private static DateOnly FirstOfMonth(DateOnly date)
        => new(date.Year, date.Month, 1);

    private static string? FormatValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateOnly date)
        {
            return date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(
                null,
                CultureInfo.InvariantCulture);
        }

        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch
        {
            return value.ToString();
        }
    }
}
