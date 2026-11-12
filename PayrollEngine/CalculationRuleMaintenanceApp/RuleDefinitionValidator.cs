using System.Text.Json;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public sealed class RuleDefinitionValidator
{
    private readonly OperationCatalogDefinition _catalog;
    private readonly PayrollDataCatalogService _dataCatalog;
    private readonly RuleFileStorageService _ruleStorage;

    public RuleDefinitionValidator(
        OperationCatalogService catalogService,
        PayrollDataCatalogService dataCatalog,
        RuleFileStorageService ruleStorage)
    {
        _catalog = catalogService.Catalog;
        _dataCatalog = dataCatalog;
        _ruleStorage = ruleStorage;
    }

    public async Task<IReadOnlyList<string>> ValidateAsync(
        CalculationRuleDefinition rule,
        CancellationToken cancellationToken = default)
    {
        var errors = Validate(rule).ToList();
        var requestedReferences = CollectDataReferences(rule);

        if (requestedReferences.Count > 0)
        {
            var catalogReferences = await _dataCatalog.SearchAsync(
                search: null,
                cancellationToken: cancellationToken);

            var knownReferences = catalogReferences
                .Select(reference => reference.Reference)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var requested in requestedReferences)
            {
                if (!knownReferences.Contains(requested.Reference))
                {
                    errors.Add(
                        $"{requested.Path}: ismeretlen adat-hivatkozás: " +
                        $"{requested.Reference}.");
                }
            }
        }

        var availableRules = (await _ruleStorage.GetAllAsync(cancellationToken))
            .Where(existing => !string.Equals(
                existing.RuleCode,
                rule.RuleCode,
                StringComparison.OrdinalIgnoreCase))
            .Append(rule)
            .ToDictionary(
                existing => existing.RuleCode,
                StringComparer.OrdinalIgnoreCase);

        foreach (var availableRule in availableRules.Values)
        {
            ValidateRuleCalls(availableRule, availableRules, errors);
        }
        ValidateRuleCallCycles(availableRules, errors);

        return errors;
    }

    private IReadOnlyList<string> Validate(CalculationRuleDefinition rule)
    {
        var errors = new List<string>();

        if (rule.Steps is null)
        {
            errors.Add("A szabály műveletlistája hiányzik.");
            return errors;
        }

        var isCallable = string.Equals(
            rule.RuleType,
            "callable",
            StringComparison.OrdinalIgnoreCase);

        if (!isCallable && !string.Equals(
                rule.RuleType,
                "normal",
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("A szabály típusa csak normal vagy callable lehet.");
        }

        if (rule.IsActive && !isCallable && rule.Steps.Count == 0)
        {
            errors.Add("Üres szabály nem aktiválható.");
            return errors;
        }

        if (isCallable)
        {
            var parameters = rule.Parameters ?? [];
            var duplicateParameter = parameters
                .GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateParameter is not null)
            {
                errors.Add(
                    $"A segédszabály paraméterneve többször szerepel: " +
                    $"{duplicateParameter.Key}.");
            }

            foreach (var parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Name))
                {
                    errors.Add("A segédszabály paraméterneve nem lehet üres.");
                }

                if (!IsSupportedRuleValueType(parameter.Type))
                {
                    errors.Add(
                        $"A(z) {parameter.Name} paraméter típusa nem támogatott: " +
                        $"{parameter.Type}.");
                }
            }

            if (!IsSupportedRuleValueType(rule.ReturnType))
            {
                errors.Add(
                    $"A segédszabály visszatérési típusa nem támogatott: " +
                    $"{rule.ReturnType ?? "(nincs megadva)"}.");
            }

            if (rule.ReturnExpression is not { } returnExpression ||
                returnExpression.ValueKind == JsonValueKind.Undefined)
            {
                errors.Add("A segédszabály visszatérési kifejezése kötelező.");
            }
            else
            {
                ValidateOperation(
                    returnExpression,
                    "returnExpression",
                    statementPosition: false,
                    errors);
            }

            if (ContainsOperation(rule.Steps, "emitPayrollElement"))
            {
                errors.Add(
                    "A segédszabály nem írhat közvetlenül bérelemet; " +
                    "az eredményét adja vissza a hívó szabálynak.");
            }
        }
        else if ((rule.Parameters?.Count ?? 0) > 0 || rule.ReturnExpression.HasValue ||
                 !string.IsNullOrWhiteSpace(rule.ReturnType))
        {
            errors.Add(
                "Bemeneti paraméter és visszatérési érték csak segédszabályhoz adható meg.");
        }

        for (var index = 0; index < (rule.Steps?.Count ?? 0); index++)
        {
            ValidateOperation(
                rule.Steps[index],
                $"steps[{index}]",
                statementPosition: true,
                errors);
        }

        return errors;
    }

    private bool IsSupportedRuleValueType(string? type) =>
        !string.IsNullOrWhiteSpace(type) &&
        (_catalog.ValueTypes.Contains(type, StringComparer.OrdinalIgnoreCase) ||
         string.Equals(type, "integer", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(type, "dynamic", StringComparison.OrdinalIgnoreCase));

    private void ValidateOperation(
        JsonElement node,
        string path,
        bool statementPosition,
        List<string> errors)
    {
        if (node.ValueKind != JsonValueKind.Object ||
            !node.TryGetProperty("code", out var codeElement) ||
            codeElement.ValueKind != JsonValueKind.String)
        {
            errors.Add($"{path}: hiányzik a művelet code mezője.");
            return;
        }

        var code = codeElement.GetString()!;

        var operation = _catalog.Operations.FirstOrDefault(item =>
            string.Equals(
                item.Code,
                code,
                StringComparison.OrdinalIgnoreCase));

        if (operation is null)
        {
            errors.Add($"{path}: ismeretlen műveletkód: {code}.");
            return;
        }

        var isStatement = operation.Kind is "statement" or "block";

        if (statementPosition && !isStatement)
        {
            errors.Add(
                $"{path}: a {code} művelet itt nem utasításként használható.");
        }
        else if (!statementPosition && isStatement)
        {
            errors.Add(
                $"{path}: a {code} művelet kifejezés helyén nem használható.");
        }

        if (!node.TryGetProperty("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{path}: hiányzik az inputs objektum.");
            return;
        }

        var definitions = operation.Inputs.ToDictionary(
            input => input.Name,
            StringComparer.OrdinalIgnoreCase);

        foreach (var suppliedInput in inputs.EnumerateObject())
        {
            if (!definitions.ContainsKey(suppliedInput.Name))
            {
                errors.Add(
                    $"{path}: a {code} művelet nem ismer ilyen bemenetet: " +
                    $"{suppliedInput.Name}.");
            }
        }

        foreach (var definition in operation.Inputs)
        {
            if (!inputs.TryGetProperty(definition.Name, out var value))
            {
                if (definition.Required)
                {
                    errors.Add(
                        $"{path}: hiányzik a kötelező bemenet: " +
                        $"{definition.Name}.");
                }

                continue;
            }

            ValidateInput(
                value,
                definition,
                $"{path}.inputs.{definition.Name}",
                errors);

            if (string.Equals(code, "invokeRule", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(definition.Name, "resultVariable", StringComparison.OrdinalIgnoreCase) &&
                (value.ValueKind != JsonValueKind.String ||
                 string.IsNullOrWhiteSpace(value.GetString())))
            {
                errors.Add(
                    $"{path}.inputs.resultVariable: meg kell adni a segédszabály eredményét fogadó változó nevét.");
            }

            if (string.Equals(code, "if", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(definition.Name, "condition", StringComparison.OrdinalIgnoreCase) &&
                ContainsOperation([value], "callRule"))
            {
                errors.Add(
                    $"{path}.inputs.condition: segédszabály-hívás nem használható közvetlenül az IF feltételében; " +
                    "a hívást tedd az egyik ágba, és a visszatérési értékét vizsgáld.");
            }
        }
    }

    private void ValidateInput(
        JsonElement value,
        OperationInputDefinition definition,
        string path,
        List<string> errors)
    {
        if (definition.Type == "ruleReference")
        {
            if (value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                errors.Add($"{path}: segédszabály-kód megadása kötelező.");
            }

            return;
        }

        if (definition.Type == "ruleArguments")
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: a segédszabály bemeneteit objektumban kell megadni.");
                return;
            }

            ValidateNestedOperations(value, path, errors);
            return;
        }

        if (definition.Type == "valueTypeSelector")
        {
            var selectedType = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

            if (selectedType is null ||
                !_catalog.ValueTypes.Contains(
                    selectedType,
                    StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"{path}: érvénytelen értéktípus.");
            }

            return;
        }

        if (definition.Type == "statementList")
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                errors.Add(
                    $"{path}: utasításlistának JSON-tömbnek kell lennie.");
                return;
            }

            var index = 0;

            foreach (var item in value.EnumerateArray())
            {
                ValidateOperation(
                    item,
                    $"{path}[{index}]",
                    statementPosition: true,
                    errors);

                index++;
            }

            return;
        }

        if (definition.Type == "booleanExpressionList")
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                errors.Add(
                    $"{path}: a feltétellistának JSON-tömbnek kell lennie.");
                return;
            }

            var items = value.EnumerateArray().ToArray();

            if (definition.MinimumItems.HasValue &&
                items.Length < definition.MinimumItems.Value)
            {
                errors.Add(
                    $"{path}: legalább {definition.MinimumItems.Value} " +
                    "elemet kell megadni.");
            }

            for (var index = 0; index < items.Length; index++)
            {
                ValidateOperation(
                    items[index],
                    $"{path}[{index}]",
                    statementPosition: false,
                    errors);
            }

            return;
        }

        ValidateNestedOperations(value, path, errors);
    }

    private void ValidateNestedOperations(
        JsonElement value,
        string path,
        List<string> errors)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("code", out _))
            {
                ValidateOperation(
                    value,
                    path,
                    statementPosition: false,
                    errors);

                return;
            }

            foreach (var property in value.EnumerateObject())
            {
                ValidateNestedOperations(
                    property.Value,
                    $"{path}.{property.Name}",
                    errors);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;

            foreach (var item in value.EnumerateArray())
            {
                ValidateNestedOperations(
                    item,
                    $"{path}[{index}]",
                    errors);

                index++;
            }
        }
    }

    private static void ValidateRuleCalls(
        CalculationRuleDefinition rule,
        IReadOnlyDictionary<string, CalculationRuleDefinition> availableRules,
        List<string> errors)
    {
        foreach (var call in CollectRuleCalls(rule))
        {
            if (!availableRules.TryGetValue(call.RuleCode, out var target))
            {
                errors.Add(
                    $"{call.Path}: nem található a meghívott szabály: " +
                    $"{call.RuleCode}.");
                continue;
            }

            if (!string.Equals(
                    target.RuleType,
                    "callable",
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{call.Path}: csak segédszabály hívható meg: " +
                    $"{call.RuleCode}.");
                continue;
            }

            if (rule.IsActive && !target.IsActive)
            {
                errors.Add(
                    $"{call.Path}: az aktív szabály nem hívhat inaktív segédszabályt: " +
                    $"{call.RuleCode}.");
            }

            var supplied = call.Arguments
                .EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in target.Parameters ?? [])
            {
                if (!supplied.Contains(parameter.Name))
                {
                    errors.Add(
                        $"{call.Path}: hiányzik a(z) {parameter.Name} bemenet " +
                        $"a(z) {call.RuleCode} segédszabályhoz.");
                }
            }

            foreach (var argumentName in supplied)
            {
                if (!(target.Parameters ?? []).Any(parameter =>
                        string.Equals(
                            parameter.Name,
                            argumentName,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(
                        $"{call.Path}: a(z) {call.RuleCode} segédszabály nem vár " +
                        $"ilyen bemenetet: {argumentName}.");
                }
            }
        }
    }

    private static void ValidateRuleCallCycles(
        IReadOnlyDictionary<string, CalculationRuleDefinition> availableRules,
        List<string> errors)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Visit(string ruleCode)
        {
            if (active.Contains(ruleCode))
            {
                return true;
            }

            if (!visited.Add(ruleCode) ||
                !availableRules.TryGetValue(ruleCode, out var rule))
            {
                return false;
            }

            active.Add(ruleCode);
            foreach (var call in CollectRuleCalls(rule))
            {
                if (availableRules.ContainsKey(call.RuleCode) &&
                    Visit(call.RuleCode))
                {
                    return true;
                }
            }

            active.Remove(ruleCode);
            return false;
        }

        var hasCycle = availableRules.Keys.Any(Visit);
        if (hasCycle &&
            !errors.Any(error => error.Contains(
                "körkörös segédszabály-hivatkozás",
                StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("A szabályhívások között körkörös segédszabály-hivatkozás van.");
        }
    }

    private static IReadOnlyList<RuleCallReference> CollectRuleCalls(
        CalculationRuleDefinition rule)
    {
        var calls = new List<RuleCallReference>();
        for (var index = 0; index < (rule.Steps?.Count ?? 0); index++)
        {
            CollectRuleCalls(
                rule.Steps[index],
                $"steps[{index}]",
                calls);
        }

        if (rule.ReturnExpression is { } returnExpression &&
            returnExpression.ValueKind != JsonValueKind.Undefined)
        {
            CollectRuleCalls(returnExpression, "returnExpression", calls);
        }

        return calls;
    }

    private static void CollectRuleCalls(
        JsonElement node,
        string path,
        List<RuleCallReference> calls)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("code", out var code) &&
                code.ValueKind == JsonValueKind.String &&
                (string.Equals(
                    code.GetString(),
                    "callRule",
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                    code.GetString(),
                    "invokeRule",
                    StringComparison.OrdinalIgnoreCase)) &&
                node.TryGetProperty("inputs", out var inputs) &&
                inputs.ValueKind == JsonValueKind.Object &&
                inputs.TryGetProperty("ruleCode", out var ruleCode) &&
                ruleCode.ValueKind == JsonValueKind.String &&
                inputs.TryGetProperty("arguments", out var arguments) &&
                arguments.ValueKind == JsonValueKind.Object)
            {
                calls.Add(new RuleCallReference(
                    $"{path}.inputs.ruleCode",
                    ruleCode.GetString()!,
                    arguments));
            }

            foreach (var property in node.EnumerateObject())
            {
                CollectRuleCalls(property.Value, $"{path}.{property.Name}", calls);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in node.EnumerateArray())
            {
                CollectRuleCalls(item, $"{path}[{index}]", calls);
                index++;
            }
        }
    }

    private static bool ContainsOperation(
        IEnumerable<JsonElement> nodes,
        string operationCode)
    {
        foreach (var node in nodes)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("code", out var code) &&
                    code.ValueKind == JsonValueKind.String &&
                    string.Equals(
                        code.GetString(),
                        operationCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (node.EnumerateObject().Any(property =>
                        ContainsOperation([property.Value], operationCode)))
                {
                    return true;
                }
            }
            else if (node.ValueKind == JsonValueKind.Array &&
                     ContainsOperation(node.EnumerateArray(), operationCode))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record RuleCallReference(
        string Path,
        string RuleCode,
        JsonElement Arguments);

    private static List<RequestedDataReference> CollectDataReferences(
        CalculationRuleDefinition rule)
    {
        var references = new List<RequestedDataReference>();

        if (rule.Steps is null)
        {
            return references;
        }

        for (var index = 0; index < rule.Steps.Count; index++)
        {
            CollectDataReferences(
                rule.Steps[index],
                $"steps[{index}]",
                references);
        }

        if (rule.ReturnExpression is { } returnExpression &&
            returnExpression.ValueKind != JsonValueKind.Undefined)
        {
            CollectDataReferences(
                returnExpression,
                "returnExpression",
                references);
        }

        return references;
    }

    private static void CollectDataReferences(
        JsonElement node,
        string path,
        List<RequestedDataReference> references)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("code", out var codeElement) &&
                codeElement.ValueKind == JsonValueKind.String &&
                string.Equals(
                    codeElement.GetString(),
                    "dataReference",
                    StringComparison.OrdinalIgnoreCase) &&
                node.TryGetProperty("inputs", out var inputs) &&
                inputs.ValueKind == JsonValueKind.Object &&
                inputs.TryGetProperty("reference", out var referenceElement) &&
                referenceElement.ValueKind == JsonValueKind.String)
            {
                var reference = referenceElement.GetString();

                if (!string.IsNullOrWhiteSpace(reference))
                {
                    references.Add(new RequestedDataReference(
                        $"{path}.inputs.reference",
                        reference));
                }
            }

            foreach (var property in node.EnumerateObject())
            {
                CollectDataReferences(
                    property.Value,
                    $"{path}.{property.Name}",
                    references);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            var index = 0;

            foreach (var item in node.EnumerateArray())
            {
                CollectDataReferences(
                    item,
                    $"{path}[{index}]",
                    references);

                index++;
            }
        }
    }

    private sealed record RequestedDataReference(
        string Path,
        string Reference);
}
