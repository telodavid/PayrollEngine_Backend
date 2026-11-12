using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace PayrollEngine.CalculationRuleMaintenanceApp.Execution;

public sealed class OperationEvaluator
{
    public object? EvaluateExpression(
        JsonElement operation,
        RuleExecutionContext context)
    {
        var code = GetCode(operation);
        var inputs = GetInputs(operation);

        var result = code switch
        {
            "constant" => EvaluateConstant(inputs),
            "dataReference" => context.ReadData(
                GetRequiredString(inputs, "reference")),
            "workCalendar" => context.ReadData("PAYROLL_RUN.WorkCalendar"),
            "calendarEntries" => ReadCalendarEntries(context),
            "payrollElementValues" => context.GetPayrollElementValues(),
            "variableReference" => context.ReadVariable(
                GetRequiredString(inputs, "name")),
            "payrollElementValue" => context.ReadPayrollElementValue(
                GetRequiredString(inputs, "payrollElement")),
            "calculatedPayrollElementValue" => context.ReadCalculatedPayrollElementValue(
                GetRequiredString(inputs, "payrollElement")),
            "callRule" => EvaluateCallableRule(inputs, context),

            "equal" => ValuesEqual(
                EvaluateRequiredInput(inputs, "left", context),
                EvaluateRequiredInput(inputs, "right", context)),
            "notEqual" => ValuesNotEqual(
                EvaluateRequiredInput(inputs, "left", context),
                EvaluateRequiredInput(inputs, "right", context)),
            "greaterThan" => CompareInputs(inputs, context) > 0,
            "greaterThanOrEqual" => CompareInputs(inputs, context) >= 0,
            "lessThan" => CompareInputs(inputs, context) < 0,
            "lessThanOrEqual" => CompareInputs(inputs, context) <= 0,

            "contains" => ContainsValue(
                EvaluateRequiredInput(inputs, "value", context),
                EvaluateRequiredInput(inputs, "searchText", context)),
            "startsWith" => StartsWithValue(
                EvaluateRequiredInput(inputs, "value", context),
                EvaluateRequiredInput(inputs, "prefix", context)),
            "isNull" => IsNullValue(
                EvaluateRequiredInput(inputs, "value", context)),
            "isNotNull" => IsNotNullValue(
                EvaluateRequiredInput(inputs, "value", context)),

            "and" => EvaluateAnd(inputs, context),
            "or" => EvaluateOr(inputs, context),
            "not" => EvaluateNot(inputs, context),

            "add" => EvaluateDecimal(inputs, "left", context)
                + EvaluateDecimal(inputs, "right", context),
            "subtract" => EvaluateDecimal(inputs, "left", context)
                - EvaluateDecimal(inputs, "right", context),
            "multiply" => EvaluateDecimal(inputs, "left", context)
                * EvaluateDecimal(inputs, "right", context),
            "divide" => Divide(inputs, context),
            "round" => Round(inputs, context),
            "dateRange" => DateRange(inputs, context),
            "dateDifferenceDays" => DateDifferenceDays(inputs, context),
            "completedYears" => CompletedYears(inputs, context),
            "dayOfWeek" => DayOfWeek(inputs, context),
            "modulo" => Modulo(inputs, context),
            "durationHours" => DurationHours(inputs, context),
            "overlapHours" => OverlapHours(inputs, context),

            "count" => EvaluateCollection(
                    GetRequiredInput(inputs, "items"),
                    context)
                .Count,
            "sum" => Sum(inputs, context),
            "any" => Any(inputs, context),

            _ => throw new InvalidOperationException(
                $"A „{code}” nem támogatott kifejezésművelet.")
        };

        var description = code switch
        {
            "dataReference" => GetRequiredString(inputs, "reference"),
            "variableReference" => $"Változó: {GetRequiredString(inputs, "name")}",
            "payrollElementValue" =>
                $"Hozzárendelt bérelem: {GetRequiredString(inputs, "payrollElement")}",
            "calculatedPayrollElementValue" =>
                $"Számított bérelem: {GetRequiredString(inputs, "payrollElement")}",
            "constant" => $"Konstans ({GetRequiredString(inputs, "valueType")})",
            _ => null
        };

        context.RecordOperation(code, "Evaluated", result, description);
        return result;
    }

    public void ExecuteStatement(
        JsonElement operation,
        RuleExecutionContext context)
    {
        var code = GetCode(operation);
        var inputs = GetInputs(operation);

        switch (code)
        {
            case "setVariable":
                {
                    var name = GetRequiredString(inputs, "name");
                    var value = EvaluateRequiredInput(inputs, "value", context);
                    if (!IsMissing(value))
                    {
                        context.SetVariable(name, value);
                    }
                    break;
                }

            case "invokeRule":
                {
                    var result = EvaluateCallableRule(inputs, context);
                    var resultVariable = GetRequiredString(inputs, "resultVariable");
                    context.SetVariable(resultVariable, result);
                    break;
                }

            case "if":
                ExecuteIf(inputs, context);
                return;

            case "switch":
                ExecuteSwitch(inputs, context);
                break;

            case "forEach":
                ExecuteForEach(inputs, context);
                break;

            case "emitPayrollElement":
                {
                    var payrollElement = GetRequiredString(inputs, "payrollElement");
                    var amount = EvaluateDecimal(inputs, "amount", context);
                    context.AddPayrollElementResult(payrollElement, amount);
                    break;
                }

            default:
                throw new InvalidOperationException(
                    $"A „{code}” nem támogatott utasításművelet.");
        }

        context.RecordOperation(code, "Executed", null);
    }

    private object? EvaluateConstant(JsonElement inputs)
    {
        var valueType = GetRequiredString(inputs, "valueType");
        var value = GetRequiredInput(inputs, "value");

        return valueType.ToLowerInvariant() switch
        {
            "text" => ReadText(value),
            "decimal" => ReadDecimal(value),
            "date" => ReadDate(value),
            "time" => ReadTime(value),
            "boolean" => ReadBoolean(value),
            "option" => ReadUntypedValue(value),
            "collection" => ReadCollection(value),
            _ => throw new InvalidOperationException(
                $"Ismeretlen konstans típus: „{valueType}”.")
        };
    }

    private void ExecuteIf(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var condition = EvaluateBooleanExpression(
            GetRequiredInput(inputs, "condition"),
            context);

        var branchName = condition ? "thenSteps" : "elseSteps";
        var branchLabel = condition
            ? "Ha ág kiválasztva (feltétel igaz)"
            : "Egyébként ág kiválasztva (feltétel hamis)";

        context.RecordOperation("if", "BranchSelected", condition, branchLabel);

        if (inputs.TryGetProperty(branchName, out var steps))
        {
            using (context.PushTraceContext(branchLabel))
            {
                ExecuteStatementList(steps, context, branchName);
            }
        }
    }

    private void ExecuteSwitch(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var switchValue = EvaluateRequiredInput(inputs, "value", context);
        if (IsMissing(switchValue))
        {
            return;
        }

        var cases = GetRequiredInput(inputs, "cases");

        if (cases.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "A switch cases bemenetének JSON-tömbnek kell lennie.");
        }

        foreach (var caseItem in cases.EnumerateArray())
        {
            if (caseItem.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "A switch minden esetének JSON-objektumnak kell lennie.");
            }

            if (!TryGetProperty(caseItem, "value", "caseValue", out var caseValue))
            {
                throw new InvalidOperationException(
                    "A switch esetből hiányzik a value mező.");
            }

            var evaluatedCaseValue = EvaluateInput(caseValue, context);

            if (!ValuesEqual(switchValue, evaluatedCaseValue))
            {
                continue;
            }

            if (!TryGetProperty(caseItem, "steps", "thenSteps", out var steps))
            {
                throw new InvalidOperationException(
                    "A switch esetből hiányzik a steps lista.");
            }

            ExecuteStatementList(steps, context, "switch case steps");
            return;
        }

        if (inputs.TryGetProperty("defaultSteps", out var defaultSteps))
        {
            ExecuteStatementList(defaultSteps, context, "defaultSteps");
        }
    }

    private void ExecuteForEach(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var items = EvaluateCollection(
            GetRequiredInput(inputs, "items"),
            context);
        var itemVariable = GetRequiredString(inputs, "itemVariable");
        var steps = GetRequiredInput(inputs, "steps");

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            using (context.PushVariableScope())
            {
                context.SetScopedVariable(itemVariable, item);

                using (context.PushTraceContext(
                    DescribeLoopItem(itemVariable, item, index + 1, items.Count)))
                {
                    ExecuteStatementList(steps, context, "forEach steps");
                }
            }
        }
    }

    private bool EvaluateAnd(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var conditions = GetRequiredInput(inputs, "conditions");
        var items = GetArrayItems(conditions, "conditions");

        foreach (var condition in items)
        {
            if (!EvaluateBooleanExpression(condition, context))
            {
                return false;
            }
        }

        return true;
    }

    private bool EvaluateOr(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var conditions = GetRequiredInput(inputs, "conditions");
        var items = GetArrayItems(conditions, "conditions");

        foreach (var condition in items)
        {
            if (EvaluateBooleanExpression(condition, context))
            {
                return true;
            }
        }

        return false;
    }

    private decimal Sum(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var items = EvaluateCollection(
            GetRequiredInput(inputs, "items"),
            context);
        var valueExpression = GetRequiredInput(inputs, "value");
        decimal total = 0;
        var index = 0;

        foreach (var item in items)
        {
            index++;

            using (context.PushVariableScope())
            {
                context.SetScopedVariable("item", item);

                using (context.PushTraceContext(
                    DescribeLoopItem("item", item, index, items.Count)))
                {
                    try
                    {
                        var value = EvaluateInput(valueExpression, context);

                        if (!IsMissing(value) && value is not null)
                        {
                            total += ToDecimal(value);
                        }
                    }
                    catch (MissingPayrollValueException)
                    {
                        // A hiányos adatsor nem járul hozzá az összeghez.
                    }
                }
            }
        }

        return total;
    }

    private object? EvaluateCallableRule(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var ruleCode = GetRequiredString(inputs, "ruleCode");
        var rule = context.GetCallableRule(ruleCode);
        var argumentsElement = GetRequiredInput(inputs, "arguments");

        if (argumentsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "A segédszabály arguments bemenetének JSON-objektumnak kell lennie.");
        }

        var parameters = rule.Parameters ?? [];
        var definitions = parameters.ToDictionary(
            parameter => parameter.Name,
            StringComparer.OrdinalIgnoreCase);
        var arguments = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var suppliedArgument in argumentsElement.EnumerateObject())
        {
            if (!definitions.TryGetValue(suppliedArgument.Name, out var parameter))
            {
                throw new InvalidOperationException(
                    $"A(z) {ruleCode} segédszabály nem vár ilyen bemenetet: " +
                    $"{suppliedArgument.Name}.");
            }

            var value = EvaluateInput(suppliedArgument.Value, context);
            EnsureCallableValueType(
                value,
                parameter.Type,
                $"A(z) {parameter.Name} bemenet");
            arguments.Add(parameter.Name, value);
        }

        foreach (var parameter in parameters)
        {
            if (!arguments.ContainsKey(parameter.Name))
            {
                throw new InvalidOperationException(
                    $"Hiányzik a(z) {ruleCode} segédszabály bemenete: " +
                    $"{parameter.Name}.");
            }
        }

        if (rule.ReturnExpression is not { } returnExpression ||
            returnExpression.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException(
                $"A(z) {ruleCode} segédszabályból hiányzik a visszatérési kifejezés.");
        }

        var previousRuleCode = context.CurrentRuleCode;
        using (context.PushRuleInvocationScope(ruleCode, arguments))
        {
            try
            {
                context.CurrentRuleCode = rule.RuleCode;

                foreach (var step in rule.Steps ?? [])
                {
                    try
                    {
                        ExecuteStatement(step, context);
                    }
                    catch (MissingPayrollValueException)
                    {
                        context.RecordOperation(
                            GetCode(step),
                            "SkippedMissingValue",
                            null);
                    }
                }

                var result = EvaluateInput(returnExpression, context);
                EnsureCallableValueType(
                    result,
                    rule.ReturnType ?? string.Empty,
                    $"A(z) {ruleCode} visszatérési értéke");
                return result;
            }
            finally
            {
                context.CurrentRuleCode = previousRuleCode;
            }
        }
    }

    private static object? ReadWorkCalendar(RuleExecutionContext context)
    {
        context.PrepareWorkCalendar();
        return context.ReadData("PAYROLL_RUN.WorkCalendar");
    }

    private static object? ReadCalendarEntries(RuleExecutionContext context)
    {
        context.PrepareWorkCalendar();
        return context.ReadData("PAYROLL_RUN.CalendarEntries");
    }

    private static void EnsureCallableValueType(
        object? value,
        string expectedType,
        string label)
    {
        if (IsMissing(value) || value is null)
        {
            throw new InvalidOperationException(
                $"{label} hiányzik.");
        }

        var valid = expectedType.ToLowerInvariant() switch
        {
            "dynamic" => true,
            "text" => value is string,
            "decimal" => TryToDecimal(value, out _),
            "integer" => TryToDecimal(value, out var number) &&
                         number == decimal.Truncate(number),
            "date" => TryToDateOnly(value, out _),
            "time" => TryToTimeOnly(value, out _),
            "boolean" => value is bool,
            "option" => value is string or int or long or decimal,
            "collection" => value is IEnumerable and not string,
            _ => false
        };

        if (!valid)
        {
            throw new InvalidOperationException(
                $"{label} típusa nem egyezik a megadott típussal ({expectedType}).");
        }
    }

    private bool Any(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var items = EvaluateCollection(
            GetRequiredInput(inputs, "items"),
            context);
        var condition = GetRequiredInput(inputs, "condition");
        var index = 0;

        foreach (var item in items)
        {
            index++;

            using (context.PushVariableScope())
            {
                context.SetScopedVariable("item", item);

                using (context.PushTraceContext(
                    DescribeLoopItem("item", item, index, items.Count)))
                {
                    if (EvaluateBooleanExpression(condition, context))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private IReadOnlyList<object?> DateRange(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var start = ToDateOnly(EvaluateRequiredInput(inputs, "start", context));
        var end = ToDateOnly(EvaluateRequiredInput(inputs, "end", context));

        if (end < start)
        {
            throw new InvalidOperationException(
                "A dátumtartomány vége nem lehet korábbi a kezdeténél.");
        }

        var dayCount = end.DayNumber - start.DayNumber + 1;

        if (dayCount > 366)
        {
            throw new InvalidOperationException(
                "Egy szabály legfeljebb 366 napos dátumtartományt dolgozhat fel.");
        }

        return Enumerable.Range(0, dayCount)
            .Select(offset => (object?)start.AddDays(offset))
            .ToArray();
    }

    private decimal DateDifferenceDays(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var start = ToDateOnly(EvaluateRequiredInput(inputs, "start", context));
        var end = ToDateOnly(EvaluateRequiredInput(inputs, "end", context));
        return end.DayNumber - start.DayNumber;
    }

    private decimal CompletedYears(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var start = ToDateOnly(EvaluateRequiredInput(inputs, "start", context));
        var end = ToDateOnly(EvaluateRequiredInput(inputs, "end", context));

        if (end < start)
        {
            throw new InvalidOperationException(
                "A záró dátum nem lehet korábbi a kezdő dátumnál az eltelt évek számításakor.");
        }

        var years = end.Year - start.Year;

        if (start.AddYears(years) > end)
        {
            years--;
        }

        return years;
    }

    private decimal DayOfWeek(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var date = ToDateOnly(EvaluateRequiredInput(inputs, "date", context));
        return ((int)date.DayOfWeek + 6) % 7 + 1;
    }

    private decimal Modulo(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var value = EvaluateDecimal(inputs, "value", context);
        var divisor = EvaluateDecimal(inputs, "divisor", context);

        if (divisor == 0)
        {
            throw new DivideByZeroException(
                "A maradékképzés osztója nem lehet nulla.");
        }

        var remainder = value % divisor;
        return remainder < 0 ? remainder + Math.Abs(divisor) : remainder;
    }

    private decimal DurationHours(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var start = ToTimeSpan(EvaluateRequiredInput(inputs, "start", context));
        var end = ToTimeSpan(EvaluateRequiredInput(inputs, "end", context));
        var minutes = end.TotalMinutes - start.TotalMinutes;

        if (minutes < 0 ||
            (minutes == 0 &&
             inputs.TryGetProperty("overnightWhenEqual", out var overnight) &&
             overnight.ValueKind == JsonValueKind.True))
        {
            minutes += 24 * 60;
        }

        return (decimal)(minutes / 60d);
    }

    private decimal OverlapHours(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var start = ToTimeSpan(EvaluateRequiredInput(inputs, "start", context)).TotalMinutes;
        var end = ToTimeSpan(EvaluateRequiredInput(inputs, "end", context)).TotalMinutes;
        var windowStart = ToTimeSpan(EvaluateRequiredInput(inputs, "windowStart", context)).TotalMinutes;
        var windowEnd = ToTimeSpan(EvaluateRequiredInput(inputs, "windowEnd", context)).TotalMinutes;

        if (end < start)
        {
            end += 24 * 60;
        }

        if (windowEnd < windowStart)
        {
            windowEnd += 24 * 60;
        }

        var overlap = Math.Max(
            0d,
            Math.Min(end, windowEnd) - Math.Max(start, windowStart));

        // A napi intervallumok éjfél körüli eltolását is figyelembe vesszük.
        overlap = Math.Max(
            overlap,
            Math.Max(
                0d,
                Math.Min(end, windowEnd + 24 * 60) -
                Math.Max(start, windowStart + 24 * 60)));

        overlap = Math.Max(
            overlap,
            Math.Max(
                0d,
                Math.Min(end + 24 * 60, windowEnd) -
                Math.Max(start + 24 * 60, windowStart)));

        return (decimal)(overlap / 60d);
    }

    private static DateOnly ToDateOnly(object? value)
    {
        if (TryToDateOnly(value, out var date))
        {
            return date;
        }

        if (value is string text &&
            DateOnly.TryParse(text, CultureInfo.InvariantCulture, out date))
        {
            return date;
        }

        throw new InvalidOperationException(
            "A művelet bemenete nem érvényes dátum.");
    }

    private static TimeSpan ToTimeSpan(object? value)
    {
        if (value is TimeSpan time)
        {
            return time;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.TimeOfDay;
        }

        if (value is string text &&
            TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsedTime))
        {
            return parsedTime;
        }

        throw new InvalidOperationException(
            "A művelet bemenete nem érvényes időpont.");
    }

    private decimal Divide(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var left = EvaluateDecimal(inputs, "left", context);
        var right = EvaluateDecimal(inputs, "right", context);

        if (right == 0)
        {
            throw new DivideByZeroException(
                "A számítási szabály nullával próbált osztani.");
        }

        return left / right;
    }

    private decimal Round(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var value = EvaluateDecimal(inputs, "value", context);
        var digitsValue = EvaluateRequiredInput(inputs, "digits", context);
        var digits = ToInteger(digitsValue);

        if (digits is < 0 or > 28)
        {
            throw new InvalidOperationException(
                "A kerekítés tizedesjegyeinek száma 0 és 28 közé kell essen.");
        }

        return decimal.Round(value, digits);
    }

    private int CompareInputs(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var left = EvaluateRequiredInput(inputs, "left", context);
        var right = EvaluateRequiredInput(inputs, "right", context);

        if (IsMissing(left) || IsMissing(right))
        {
            throw new MissingPayrollValueException();
        }

        if (left is null || right is null)
        {
            throw new InvalidOperationException(
                "Null értékek nem használhatók rendezett összehasonlításban.");
        }

        if (TryToDecimal(left, out var leftNumber) &&
            TryToDecimal(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (TryToDateOnly(left, out var leftDate) &&
            TryToDateOnly(right, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        if (left is string leftText && right is string rightText)
        {
            return string.Compare(
                leftText,
                rightText,
                StringComparison.OrdinalIgnoreCase);
        }

        if (left is TimeSpan leftTime && right is TimeSpan rightTime)
        {
            return leftTime.CompareTo(rightTime);
        }

        throw new InvalidOperationException(
            $"A két érték nem hasonlítható össze: {left.GetType().Name} és {right.GetType().Name}.");
    }

    private bool EvaluateBooleanExpression(
        JsonElement operation,
        RuleExecutionContext context)
    {
        try
        {
            var value = EvaluateExpression(operation, context);
            return IsMissing(value) ? false : ToBoolean(value);
        }
        catch (MissingPayrollValueException)
        {
            return false;
        }
    }

    private bool EvaluateNot(
        JsonElement inputs,
        RuleExecutionContext context)
    {
        var condition = EvaluateRequiredInput(inputs, "condition", context);
        return IsMissing(condition) || condition is null
            ? false
            : !ToBoolean(condition);
    }

    private decimal EvaluateDecimal(
        JsonElement inputs,
        string name,
        RuleExecutionContext context)
    {
        return ToDecimal(EvaluateRequiredInput(inputs, name, context));
    }

    private object? EvaluateRequiredInput(
        JsonElement inputs,
        string name,
        RuleExecutionContext context)
    {
        return EvaluateInput(GetRequiredInput(inputs, name), context);
    }

    private object? EvaluateInput(
        JsonElement input,
        RuleExecutionContext context)
    {
        if (input.ValueKind == JsonValueKind.Object &&
            input.TryGetProperty("code", out _))
        {
            return EvaluateExpression(input, context);
        }

        return ReadUntypedValue(input);
    }

    private IReadOnlyList<object?> EvaluateCollection(
        JsonElement input,
        RuleExecutionContext context)
    {
        return ToCollection(EvaluateInput(input, context));
    }

    private static IReadOnlyList<JsonElement> GetArrayItems(
        JsonElement value,
        string name)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"A „{name}” bemenetnek JSON-tömbnek kell lennie.");
        }

        return value.EnumerateArray().ToArray();
    }

    private void ExecuteStatementList(
        JsonElement steps,
        RuleExecutionContext context,
        string name)
    {
        if (steps.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"A „{name}” bemenetnek JSON-tömbnek kell lennie.");
        }

        foreach (var step in steps.EnumerateArray())
        {
            try
            {
                ExecuteStatement(step, context);
            }
            catch (MissingPayrollValueException)
            {
                context.RecordOperation(
                    GetCode(step),
                    "SkippedMissingValue",
                    null);
            }
        }
    }

    private static IReadOnlyList<object?> ToCollection(object? value)
    {
        if (IsMissing(value) || value is null)
        {
            return [];
        }

        if (value is string || value is not IEnumerable enumerable)
        {
            throw new InvalidOperationException(
                "A bemenetnek lista vagy tömb típusúnak kell lennie.");
        }

        var items = new List<object?>();

        foreach (var item in enumerable)
        {
            items.Add(item);
        }

        return items;
    }

    private static string DescribeLoopItem(
        string variableName,
        object? item,
        int index,
        int count)
    {
        var description = new List<string>
        {
            $"{variableName} {index}/{count}"
        };

        if (item is IReadOnlyDictionary<string, object?> row)
        {
            AddLoopField(description, row, ".Date", "Dátum");
            AddLoopField(description, row, ".DayName", "Nap");
            AddLoopField(description, row, ".DayTypeName", "Naptípus");
            AddLoopField(description, row, ".TypeName", "Esemény");
            AddLoopField(description, row, ".TypeCode", "Eseménykód");
        }

        return string.Join("; ", description);
    }

    private static void AddLoopField(
        ICollection<string> description,
        IReadOnlyDictionary<string, object?> row,
        string suffix,
        string label)
    {
        var field = row.FirstOrDefault(pair =>
            pair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (field.Key is null || field.Value is null)
        {
            return;
        }

        var value = field.Value switch
        {
            DateOnly date =>
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            JsonElement json when json.ValueKind == JsonValueKind.String =>
                json.GetString(),
            IFormattable formattable =>
                formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => field.Value.ToString()
        };

        if (!string.IsNullOrWhiteSpace(value))
        {
            description.Add($"{label}: {value}");
        }
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (IsMissing(left) || IsMissing(right))
        {
            return false;
        }

        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (TryToDecimal(left, out var leftNumber) &&
            TryToDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        if (TryToDateOnly(left, out var leftDate) &&
            TryToDateOnly(right, out var rightDate))
        {
            return leftDate == rightDate;
        }

        if (left is string leftText && right is string rightText)
        {
            return string.Equals(
                leftText,
                rightText,
                StringComparison.OrdinalIgnoreCase);
        }

        return Equals(left, right);
    }

    private static bool ValuesNotEqual(object? left, object? right) =>
        !IsMissing(left) && !IsMissing(right) && !ValuesEqual(left, right);

    private static decimal ToDecimal(object? value)
    {
        if (IsMissing(value) || value is null)
        {
            throw new MissingPayrollValueException();
        }

        if (TryToDecimal(value, out var number))
        {
            return number;
        }

        throw new InvalidOperationException(
            $"Az érték nem alakítható decimális számmá: {FormatValue(value)}.");
    }

    private static bool TryToDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal number:
                result = number;
                return true;
            case byte number:
                result = number;
                return true;
            case sbyte number:
                result = number;
                return true;
            case short number:
                result = number;
                return true;
            case ushort number:
                result = number;
                return true;
            case int number:
                result = number;
                return true;
            case uint number:
                result = number;
                return true;
            case long number:
                result = number;
                return true;
            case ulong number:
                result = number;
                return true;
            case float number:
                result = (decimal)number;
                return true;
            case double number:
                result = (decimal)number;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static int ToInteger(object? value)
    {
        var number = ToDecimal(value);

        if (number != decimal.Truncate(number))
        {
            throw new InvalidOperationException(
                "Egész számot kell megadni.");
        }

        return checked((int)number);
    }

    private static bool ToBoolean(object? value)
    {
        if (value is bool boolean)
        {
            return boolean;
        }

        throw new InvalidOperationException(
            $"A feltétel eredményének logikai értéknek kell lennie, kapott érték: {FormatValue(value)}.");
    }

    private static bool TryToDateOnly(object? value, out DateOnly result)
    {
        switch (value)
        {
            case DateOnly date:
                result = date;
                return true;
            case DateTime dateTime:
                result = DateOnly.FromDateTime(dateTime);
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static object? ReadText(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                "A text konstans értékének szövegnek kell lennie.");
        }

        return value.GetString();
    }

    private static decimal ReadDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out number))
        {
            return number;
        }

        throw new InvalidOperationException(
            "A decimal konstans értéke nem értelmezhető számként.");
    }

    private static TimeSpan ReadTime(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String &&
            TimeSpan.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                out var time) &&
            time >= TimeSpan.Zero &&
            time < TimeSpan.FromDays(1))
        {
            return time;
        }

        throw new InvalidOperationException(
            "A time konstans értéke nem érvényes időpont.");
    }

    private static bool TryToTimeOnly(object? value, out TimeSpan result)
    {
        switch (value)
        {
            case TimeSpan time when time >= TimeSpan.Zero &&
                                    time < TimeSpan.FromDays(1):
                result = time;
                return true;
            case DateTime dateTime:
                result = dateTime.TimeOfDay;
                return true;
            case string text when TimeSpan.TryParse(
                text,
                CultureInfo.InvariantCulture,
                out var parsed) &&
                parsed >= TimeSpan.Zero &&
                parsed < TimeSpan.FromDays(1):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static DateOnly ReadDate(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw new InvalidOperationException(
            "A date konstans értéke nem érvényes dátum.");
    }

    private static bool ReadBoolean(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        throw new InvalidOperationException(
            "A boolean konstans értéke csak true vagy false lehet.");
    }

    private static object? ReadUntypedValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Array => ReadCollection(value),
            _ => throw new InvalidOperationException(
                "A bemeneti érték formátuma nem támogatott.")
        };
    }

    private static IReadOnlyList<object?> ReadCollection(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "A collection értékének JSON-tömbnek kell lennie.");
        }

        return value
            .EnumerateArray()
            .Select(ReadUntypedValue)
            .ToArray();
    }

    private static string GetCode(JsonElement operation)
    {
        if (operation.ValueKind != JsonValueKind.Object ||
            !operation.TryGetProperty("code", out var codeElement) ||
            codeElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                "A műveletből hiányzik a szöveges „code” mező.");
        }

        return codeElement.GetString()!;
    }

    private static JsonElement GetInputs(JsonElement operation)
    {
        if (!operation.TryGetProperty("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "A műveletből hiányzik az „inputs” objektum.");
        }

        return inputs;
    }

    private static JsonElement GetRequiredInput(
        JsonElement inputs,
        string name)
    {
        if (!inputs.TryGetProperty(name, out var value))
        {
            throw new InvalidOperationException(
                $"Hiányzó kötelező bemenet: „{name}”.");
        }

        return value;
    }

    private static string GetRequiredString(
        JsonElement inputs,
        string name)
    {
        var value = GetRequiredInput(inputs, name);

        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException(
                $"A „{name}” bemenetnek nem üres szövegnek kell lennie.");
        }

        return value.GetString()!;
    }

    private static bool TryGetProperty(
        JsonElement value,
        string firstName,
        string secondName,
        out JsonElement property)
    {
        return value.TryGetProperty(firstName, out property)
            || value.TryGetProperty(secondName, out property);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            MissingPayrollValue => "<hiányzó>",
            null => "null",
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool IsMissing(object? value) =>
        ReferenceEquals(value, MissingPayrollValue.Instance);

    private static bool IsNullValue(object? value) =>
        !IsMissing(value) && value is null;

    private static bool IsNotNullValue(object? value) =>
        !IsMissing(value) && value is not null;

    private static bool ContainsValue(object? source, object? search)
    {
        if (IsMissing(source) ||
            source is null ||
            IsMissing(search) ||
            search is null)
        {
            return false;
        }

        var searchText = Convert.ToString(search, CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(searchText))
        {
            return false;
        }

        if (source is string text)
        {
            return text.Contains(
                searchText,
                StringComparison.OrdinalIgnoreCase);
        }

        if (source is IEnumerable values)
        {
            foreach (var value in values)
            {
                if (value is string item &&
                    item.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool StartsWithValue(object? source, object? prefix)
    {
        if (IsMissing(source) ||
            source is null ||
            IsMissing(prefix) ||
            prefix is null)
        {
            return false;
        }

        var prefixText = Convert.ToString(prefix, CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(prefixText))
        {
            return false;
        }

        return source is string text &&
               text.StartsWith(
                   prefixText,
                   StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class MissingPayrollValueException : Exception { }