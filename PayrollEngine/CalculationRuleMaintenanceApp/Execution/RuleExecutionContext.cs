using System.Collections;
using System.Globalization;
using System.Text.Json;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp.Execution;

public sealed class RuleExecutionContext
{
    private readonly Dictionary<string, object?> _data;
    private readonly Dictionary<string, object?> _variables =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Dictionary<string, object?>> _variableScopes = [];
    private readonly List<int> _ruleScopeBoundaries = [];
    private readonly List<string> _ruleCallStack = [];
    private readonly IReadOnlyDictionary<string, CalculationRuleDefinition> _rules;
    private readonly List<CalculatedPayrollElement> _results = [];
    private readonly List<VariableChange> _variableChanges = [];
    private readonly List<OperationTraceEntry> _operationTraces = [];
    private readonly List<string> _traceContexts = [];
    private IReadOnlyList<WorkCalendarDay> _workCalendar = [];
    private IReadOnlyList<WorkCalendarEntry> _calendarEntries = [];
    private bool _workCalendarPrepared;
    private int _sequenceNumber;

    public RuleExecutionContext(
        IReadOnlyDictionary<string, object?> data,
        int? employeeId = null,
        DateOnly? periodStart = null,
        IReadOnlyList<PayrollResultSnapshot>? previousResults = null,
        IReadOnlyList<CalculationRuleDefinition>? rules = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        _data = data.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);

        EmployeeId = employeeId;
        PeriodStart = periodStart;
        PreviousResults = previousResults ?? [];
        _rules = (rules ?? [])
            .ToDictionary(
                rule => rule.RuleCode,
                StringComparer.OrdinalIgnoreCase);

        if (employeeId.HasValue)
        {
            _data["PAYROLL_RUN.EmployeeId"] = employeeId.Value;
        }

        if (periodStart.HasValue)
        {
            _data["PAYROLL_RUN.Period"] = periodStart.Value;
            _data["PAYROLL_RUN.PeriodYear"] = periodStart.Value.Year;
            _data["PAYROLL_RUN.PeriodEnd"] = new DateOnly(
                periodStart.Value.Year,
                periodStart.Value.Month,
                DateTime.DaysInMonth(periodStart.Value.Year, periodStart.Value.Month));
        }

        _data["PAYROLL_RUN.PreviousResults"] = PreviousResults
    .Select(result => (object?)new Dictionary<string, object?>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["PAYROLL_RUN.PreviousResults.PreviousRunId"] =
            result.PreviousRunId,
        ["PAYROLL_RUN.PreviousResults.Period"] = result.Period,
        ["PAYROLL_RUN.PreviousResults.Year"] = result.Period.Year,
        ["PAYROLL_RUN.PreviousResults.PayrollElementId"] =
            result.PayrollElementId,
        ["PAYROLL_RUN.PreviousResults.PayrollElementCode"] =
            result.PayrollElementCode,
        ["PAYROLL_RUN.PreviousResults.PayrollElementName"] =
            result.PayrollElementName,
        ["PAYROLL_RUN.PreviousResults.ResultType"] =
            result.ResultType,
        ["PAYROLL_RUN.PreviousResults.Amount"] = result.Amount
    })
    .ToArray();
    }

    public string? CurrentRuleCode { get; set; }

    public int? EmployeeId { get; }
    public DateOnly? PeriodStart { get; }
    public IReadOnlyList<PayrollResultSnapshot> PreviousResults { get; }

    public IReadOnlyList<CalculatedPayrollElement> Results => _results;
    public IReadOnlyList<VariableChange> VariableChanges => _variableChanges;
    public IReadOnlyList<OperationTraceEntry> OperationTraces => _operationTraces;
    public IReadOnlyList<WorkCalendarDay> WorkCalendar => _workCalendar;
    public IReadOnlyList<WorkCalendarEntry> CalendarEntries => _calendarEntries;

    public void PrepareWorkCalendar()
    {
        if (_workCalendarPrepared)
        {
            return;
        }

        if (PeriodStart is null)
        {
            throw new InvalidOperationException(
                "A munkaidő-naptárhoz szükséges a számfejtési periódus.");
        }

        _workCalendar = WorkCalendarBuilder.Build(_data, PeriodStart.Value);

        _data["PAYROLL_RUN.WorkCalendar"] = _workCalendar
            .Select(day => (object?)day.ToRuleRow())
            .ToArray();

        _calendarEntries = WorkCalendarBuilder.BuildEntries(
            _data,
            _workCalendar);

        _data["PAYROLL_RUN.CalendarEntries"] = _calendarEntries
            .Select(entry => (object?)entry.ToRuleRow())
            .ToArray();

        _workCalendarPrepared = true;
    }

    public object? ReadData(string reference)
    {
        var firstVisibleScope = CurrentRuleScopeBoundary;

        for (var index = _variableScopes.Count - 1;
             index >= firstVisibleScope;
             index--)
        {
            foreach (var scopedValue in _variableScopes[index].Values.Reverse())
            {
                if (TryReadReference(scopedValue, reference, out var value))
                {
                    return value;
                }
            }
        }

        if (_data.TryGetValue(reference, out var globalValue))
        {
            return globalValue;
        }

        var matches = new List<object?>();

        foreach (var collection in _data.Values)
        {
            CollectReferenceValues(collection, reference, matches);
        }

        return matches.Count switch
        {
            0 => MissingPayrollValue.Instance,
            1 => matches[0],
            _ => matches
        };
    }

    public void SetVariable(string name, object? value)
    {
        ValidateVariableName(name);

        var previousValue = TryReadVariable(name, out var existingValue)
            ? existingValue
            : null;

        var firstVisibleScope = CurrentRuleScopeBoundary;
        var targetScope = _variableScopes
            .Skip(firstVisibleScope)
            .LastOrDefault(scope => scope.ContainsKey(name));

        if (targetScope is not null)
        {
            targetScope[name] = value;
        }
        else
        {
            if (_ruleScopeBoundaries.Count > 0)
            {
                _variableScopes[firstVisibleScope][name] = value;
            }
            else
            {
                _variables[name] = value;
            }
        }

        _variableChanges.Add(new VariableChange(
            ++_sequenceNumber,
            CurrentRuleCode,
            name,
            previousValue,
            value));
    }

    public void SetScopedVariable(string name, object? value)
    {
        ValidateVariableName(name);

        if (_variableScopes.Count == 0)
        {
            throw new InvalidOperationException(
                "Nincs aktív lokális változókörnyezet.");
        }

        var previousValue = TryReadVariable(name, out var existingValue)
            ? existingValue
            : null;

        _variableScopes[^1][name] = value;

        _variableChanges.Add(new VariableChange(
            ++_sequenceNumber,
            CurrentRuleCode,
            name,
            previousValue,
            value));
    }

    public object? ReadVariable(string name)
    {
        if (TryReadVariable(name, out var value))
        {
            return value;
        }

        return MissingPayrollValue.Instance;
    }

    public CalculationRuleDefinition GetCallableRule(string ruleCode)
    {
        if (!_rules.TryGetValue(ruleCode, out var rule))
        {
            throw new FileNotFoundException(
                $"A meghívott segédszabály nem található: {ruleCode}");
        }

        if (!string.Equals(
                rule.RuleType,
                "callable",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"A(z) {ruleCode} szabály nem meghívható segédszabály.");
        }

        if (!rule.IsActive)
        {
            throw new InvalidOperationException(
                $"A meghívott segédszabály inaktív: {ruleCode}");
        }

        return rule;
    }

    public IDisposable PushRuleInvocationScope(
        string ruleCode,
        IReadOnlyDictionary<string, object?> arguments)
    {
        if (_ruleCallStack.Count >= 32)
        {
            throw new InvalidOperationException(
                "A segédszabályok hívási mélysége meghaladta a 32 szintet.");
        }

        if (_ruleCallStack.Contains(ruleCode, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"A segédszabályok között körkörös hivatkozás található: {ruleCode}");
        }

        var boundary = _variableScopes.Count;
        var scope = new Dictionary<string, object?>(
            arguments,
            StringComparer.OrdinalIgnoreCase);

        _ruleCallStack.Add(ruleCode);
        _ruleScopeBoundaries.Add(boundary);
        _variableScopes.Add(scope);

        return new RuleInvocationScope(this, scope, boundary, ruleCode);
    }

    public object? ReadPayrollElementValue(string payrollElementCode)
    {
        if (string.IsNullOrWhiteSpace(payrollElementCode))
        {
            return MissingPayrollValue.Instance;
        }

        if (!_data.TryGetValue(
                "employee.employments.payrollElements",
                out var collection))
        {
            return MissingPayrollValue.Instance;
        }

        decimal total = 0m;
        var found = false;

        foreach (var row in EnumerateCollection(collection))
        {
            if (row is not IReadOnlyDictionary<string, object?> fields)
            {
                continue;
            }

            var code = FindField(fields, "dbo.PayrollElement.Code");

            if (code is not string codeText ||
                !string.Equals(
                    codeText,
                    payrollElementCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var amount = FindField(
                fields,
                "dbo.EmploymentPayrollElement.Amount");

            if (amount is null ||
                ReferenceEquals(amount, MissingPayrollValue.Instance))
            {
                continue;
            }

            if (!TryDecimal(amount, out var decimalAmount))
            {
                continue;
            }

            total += decimalAmount;
            found = true;
        }

        return found ? total : MissingPayrollValue.Instance;
    }

    public object? ReadCalculatedPayrollElementValue(string payrollElementCode)
    {
        if (string.IsNullOrWhiteSpace(payrollElementCode))
        {
            return MissingPayrollValue.Instance;
        }

        var result = _results.FirstOrDefault(item =>
            string.Equals(
                item.PayrollElementCode,
                payrollElementCode,
                StringComparison.OrdinalIgnoreCase));

        return result is null
            ? MissingPayrollValue.Instance
            : result.Amount;
    }
    public IReadOnlyList<object?> GetPayrollElementValues()
    {
        var propertiesByCode = GetPayrollElementProperties();
        var values = new List<object?>();

        if (_data.TryGetValue(
                "employee.employments.payrollElements",
                out var assignedElements))
        {
            foreach (var item in EnumerateCollection(assignedElements))
            {
                if (item is not IReadOnlyDictionary<string, object?> fields)
                {
                    continue;
                }

                var code = FindField(fields, "dbo.PayrollElement.Code") as string;
                var amountValue = FindField(
                    fields,
                    "dbo.EmploymentPayrollElement.Amount");

                if (string.IsNullOrWhiteSpace(code) ||
                    !TryDecimal(amountValue, out var amount))
                {
                    continue;
                }

                values.Add(CreatePayrollElementValue(
                    code,
                    amount,
                    "Hozzárendelt",
                    propertiesByCode));
            }
        }

        foreach (var result in _results)
        {
            values.Add(CreatePayrollElementValue(
                result.PayrollElementCode,
                result.Amount,
                "Számított",
                propertiesByCode));
        }

        return values;
    }

    private Dictionary<string, Dictionary<string, object?>>
        GetPayrollElementProperties()
    {
        var propertiesByCode = new Dictionary<
            string,
            Dictionary<string, (DateOnly? ValidFrom, object? Value)>>(
                StringComparer.OrdinalIgnoreCase);

        if (!_data.TryGetValue("payrollElement.customFields", out var customFields))
        {
            return new Dictionary<string, Dictionary<string, object?>>(
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (var item in EnumerateCollection(customFields))
        {
            if (item is not IReadOnlyDictionary<string, object?> fields ||
                FindField(fields, "dbo.PayrollElement.Code") is not string code ||
                string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (!propertiesByCode.TryGetValue(code, out var codeProperties))
            {
                codeProperties = new Dictionary<
                    string,
                    (DateOnly? ValidFrom, object? Value)>(
                        StringComparer.OrdinalIgnoreCase);
                propertiesByCode.Add(code, codeProperties);
            }

            var validFrom = TryDateOnly(FindField(
                fields,
                "dbo.PayrollElementFieldValue.ValidFrom"));

            foreach (var field in fields)
            {
                // A payrollElement.customFields listában minden nem dbo.*
                // hivatkozás dinamikus bérelem-tulajdonság.
                if (field.Key.StartsWith(
                        "dbo.",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!codeProperties.TryGetValue(field.Key, out var current) ||
                    (validFrom.HasValue &&
                     (!current.ValidFrom.HasValue ||
                      validFrom.Value >= current.ValidFrom.Value)) ||
                    !validFrom.HasValue)
                {
                    codeProperties[field.Key] = (validFrom, field.Value);
                }
            }
        }

        return propertiesByCode.ToDictionary(
            element => element.Key,
            element => element.Value.ToDictionary(
                property => property.Key,
                property => property.Value.Value,
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, object?> CreatePayrollElementValue(
        string code,
        decimal amount,
        string source,
        IReadOnlyDictionary<string, Dictionary<string, object?>> propertiesByCode)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PAYROLL_RUN.PayrollElementValues.Code"] = code,
            ["PAYROLL_RUN.PayrollElementValues.Amount"] = amount,
            ["PAYROLL_RUN.PayrollElementValues.Source"] = source
        };
        foreach (var propertyName in propertiesByCode.Values
             .SelectMany(properties => properties.Keys)
             .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            row[propertyName] = MissingPayrollValue.Instance;
        }

        if (propertiesByCode.TryGetValue(code, out var properties))
        {
            foreach (var property in properties)
            {
                row[property.Key] = property.Value;
            }
        }

        return row;
    }

    private static DateOnly? TryDateOnly(object? value)
    {
        if (value is DateOnly date)
        {
            return date;
        }

        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }

        return DateOnly.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    public IReadOnlyDictionary<string, object?> GetVariableSnapshot()
    {
        var snapshot = new Dictionary<string, object?>(
            _variables,
            StringComparer.OrdinalIgnoreCase);

        foreach (var scope in _variableScopes)
        {
            foreach (var item in scope)
            {
                snapshot[item.Key] = item.Value;
            }
        }

        return snapshot;
    }

    public IDisposable PushVariableScope()
    {
        var scope = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);

        _variableScopes.Add(scope);
        return new VariableScope(this, scope);
    }

    public void AddPayrollElementResult(
        string payrollElementCode,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(payrollElementCode))
        {
            throw new ArgumentException(
                "A bérelem kódja nem lehet üres.",
                nameof(payrollElementCode));
        }

        var existingIndex = _results.FindIndex(result =>
            string.Equals(
                result.PayrollElementCode,
                payrollElementCode,
                StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            var existing = _results[existingIndex];

            _results[existingIndex] = existing with
            {
                Amount = existing.Amount + amount
            };
        }
        else
        {
            _results.Add(new CalculatedPayrollElement(
                payrollElementCode,
                amount,
                CurrentRuleCode,
                ++_sequenceNumber));
        }
    }

    public void RecordOperation(
     string operationCode,
     string status,
     object? result,
     string? description = null)
    {
        _operationTraces.Add(new OperationTraceEntry(
            ++_sequenceNumber,
            CurrentRuleCode,
            operationCode,
            status,
            FormatValue(result),
            description,
            _traceContexts.Count == 0
                ? null
                : string.Join(" > ", _traceContexts)));
    }

    public IDisposable PushTraceContext(string description)
    {
        _traceContexts.Add(description);
        return new TraceContextScope(this, description);
    }

    private void PopTraceContext(string description)
    {
        if (_traceContexts.Count == 0 ||
            !string.Equals(
                _traceContexts[^1],
                description,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A műveletnapló környezeteinek sorrendje érvénytelen.");
        }

        _traceContexts.RemoveAt(_traceContexts.Count - 1);
    }

    private static bool TryReadReference(
        object? source,
        string reference,
        out object? value)
    {
        if (source is IReadOnlyDictionary<string, object?> dictionary)
        {
            foreach (var item in dictionary)
            {
                if (string.Equals(
                        item.Key,
                        reference,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    private static void CollectReferenceValues(
        object? source,
        string reference,
        List<object?> matches)
    {
        if (source is IReadOnlyDictionary<string, object?> dictionary)
        {
            if (TryReadReference(dictionary, reference, out var value))
            {
                matches.Add(value);
                return;
            }

            foreach (var nested in dictionary.Values)
            {
                CollectReferenceValues(nested, reference, matches);
            }

            return;
        }

        foreach (var item in EnumerateCollection(source))
        {
            CollectReferenceValues(item, reference, matches);
        }
    }

    private static IEnumerable<object?> EnumerateCollection(object? value)
    {
        if (value is string || value is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (var item in enumerable)
        {
            yield return item;
        }
    }

    private static object? FindField(
        IReadOnlyDictionary<string, object?> fields,
        string name)
    {
        foreach (var item in fields)
        {
            if (string.Equals(
                    item.Key,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return item.Value;
            }
        }

        return MissingPayrollValue.Instance;
    }

    private static bool TryDecimal(object? value, out decimal result)
    {
        try
        {
            result = Convert.ToDecimal(
                value,
                CultureInfo.InvariantCulture);

            return true;
        }
        catch (Exception) when (value is not null)
        {
            result = 0m;
            return false;
        }
    }

    private bool TryReadVariable(string name, out object? value)
    {
        var firstVisibleScope = CurrentRuleScopeBoundary;

        for (var index = _variableScopes.Count - 1;
             index >= firstVisibleScope;
             index--)
        {
            if (_variableScopes[index].TryGetValue(name, out value))
            {
                return true;
            }
        }

        if (_ruleScopeBoundaries.Count > 0)
        {
            value = null;
            return false;
        }

        return _variables.TryGetValue(name, out value);
    }

    private int CurrentRuleScopeBoundary =>
        _ruleScopeBoundaries.Count == 0
            ? 0
            : _ruleScopeBoundaries[^1];

    private void PopVariableScope(Dictionary<string, object?> scope)
    {
        if (_variableScopes.Count == 0 ||
            !ReferenceEquals(_variableScopes[^1], scope))
        {
            throw new InvalidOperationException(
                "A lokális változókörnyezetek sorrendje érvénytelen.");
        }

        _variableScopes.RemoveAt(_variableScopes.Count - 1);
    }

    private void PopRuleInvocationScope(
        Dictionary<string, object?> scope,
        int boundary,
        string ruleCode)
    {
        PopVariableScope(scope);

        if (_ruleScopeBoundaries.Count == 0 ||
            _ruleScopeBoundaries[^1] != boundary ||
            _ruleCallStack.Count == 0 ||
            !string.Equals(
                _ruleCallStack[^1],
                ruleCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A segédszabály hívási környezeteinek sorrendje érvénytelen.");
        }

        _ruleScopeBoundaries.RemoveAt(_ruleScopeBoundaries.Count - 1);
        _ruleCallStack.RemoveAt(_ruleCallStack.Count - 1);
    }

    private static void ValidateVariableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "A változó neve nem lehet üres.",
                nameof(name));
        }
    }

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
    private sealed class TraceContextScope(
    RuleExecutionContext context,
    string description) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            context.PopTraceContext(description);
            _disposed = true;
        }
    }

    private sealed class VariableScope(
        RuleExecutionContext context,
        Dictionary<string, object?> scope) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            context.PopVariableScope(scope);
            _disposed = true;
        }
    }

    private sealed class RuleInvocationScope(
        RuleExecutionContext context,
        Dictionary<string, object?> scope,
        int boundary,
        string ruleCode) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            context.PopRuleInvocationScope(scope, boundary, ruleCode);
            _disposed = true;
        }
    }
}

public sealed record CalculatedPayrollElement(
    string PayrollElementCode,
    decimal Amount,
    string? RuleCode = null,
    int SequenceNumber = 0);

public sealed record VariableChange(
    int SequenceNumber,
    string? RuleCode,
    string Name,
    object? PreviousValue,
    object? NewValue);

public sealed record OperationTraceEntry(
    int SequenceNumber,
    string? RuleCode,
    string OperationCode,
    string Status,
    string? Result,
    string? Description = null,
    string? ExecutionContext = null);

public sealed record PayrollResultSnapshot(
    Guid PreviousRunId,
    DateOnly Period,
    int PayrollElementId,
    string PayrollElementCode,
    string PayrollElementName,
    string ResultType,
    decimal Amount);

public sealed class MissingPayrollValue
{
    private MissingPayrollValue() { }

    public static MissingPayrollValue Instance { get; } = new();
}