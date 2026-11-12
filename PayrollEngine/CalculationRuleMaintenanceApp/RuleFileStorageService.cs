using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public sealed class RuleFileStorageService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    private readonly string _rulesDirectory;

    public RuleFileStorageService(IWebHostEnvironment environment)
    {
        _rulesDirectory = Path.Combine(
            environment.ContentRootPath,
            "CalculationRuleMaintenanceApp",
            "Rules");

        Directory.CreateDirectory(_rulesDirectory);
    }
    public async Task<CalculationRuleDefinition?> DeleteAsync(
    string ruleCode,
    CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(ruleCode);

        if (!File.Exists(path))
        {
            return null;
        }

        var referencingRules = (await GetAllAsync(cancellationToken))
            .Where(rule => !string.Equals(
                rule.RuleCode,
                ruleCode,
                StringComparison.OrdinalIgnoreCase))
            .Where(rule => ReferencesRule(rule, ruleCode))
            .Select(rule => rule.RuleCode)
            .ToArray();

        if (referencingRules.Length > 0)
        {
            throw new InvalidOperationException(
                $"A szabályt más szabályok használják: " +
                string.Join(", ", referencingRules));
        }

        CalculationRuleDefinition? rule;

        await using (var stream = File.OpenRead(path))
        {
            rule =
                await JsonSerializer.DeserializeAsync<CalculationRuleDefinition>(
                    stream,
                    JsonOptions,
                    cancellationToken);
        }

        if (rule is null)
        {
            throw new InvalidDataException(
                $"A szabályfájl üres vagy hibás: {ruleCode}");
        }

        File.Delete(path);
        return rule;
    }

    private static bool ReferencesRule(
        CalculationRuleDefinition rule,
        string ruleCode)
    {
        return (rule.Steps ?? []).Any(step => ContainsRuleCall(step, ruleCode)) ||
            rule.ReturnExpression is { } returnExpression &&
            returnExpression.ValueKind != JsonValueKind.Undefined &&
            ContainsRuleCall(returnExpression, ruleCode);
    }

    private static bool ContainsRuleCall(JsonElement node, string ruleCode)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("code", out var operationCode) &&
                operationCode.ValueKind == JsonValueKind.String &&
                (string.Equals(
                    operationCode.GetString(),
                    "callRule",
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                    operationCode.GetString(),
                    "invokeRule",
                    StringComparison.OrdinalIgnoreCase)) &&
                node.TryGetProperty("inputs", out var inputs) &&
                inputs.ValueKind == JsonValueKind.Object &&
                inputs.TryGetProperty("ruleCode", out var calledRuleCode) &&
                calledRuleCode.ValueKind == JsonValueKind.String &&
                string.Equals(
                    calledRuleCode.GetString(),
                    ruleCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return node.EnumerateObject()
                .Any(property => ContainsRuleCall(property.Value, ruleCode));
        }

        return node.ValueKind == JsonValueKind.Array &&
            node.EnumerateArray().Any(item => ContainsRuleCall(item, ruleCode));
    }

    public async Task CreateAsync(
        CalculationRuleDefinition rule,
        CancellationToken cancellationToken = default)
    {
        Validate(rule);

        var path = GetFilePath(rule.RuleCode);

        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Már létezik ilyen szabály: {rule.RuleCode}");
        }

        await WriteAsync(
            path,
            rule,
            overwrite: false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CalculationRuleDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var rules = new List<CalculationRuleDefinition>();

        foreach (var path in Directory.GetFiles(_rulesDirectory, "*.json"))
        {
            await using var stream = File.OpenRead(path);

            var rule =
                await JsonSerializer.DeserializeAsync<CalculationRuleDefinition>(
                    stream,
                    JsonOptions,
                    cancellationToken);

            if (rule is not null)
            {
                rules.Add(rule);
            }
        }

        return rules
            .OrderBy(rule => rule.RuleCode)
            .ToArray();
    }

    public async Task<CalculationRuleDefinition?> GetByCodeAsync(
        string ruleCode,
        CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(ruleCode);

        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);

        return await JsonSerializer.DeserializeAsync<CalculationRuleDefinition>(
            stream,
            JsonOptions,
            cancellationToken);
    }

    public async Task UpdateAsync(
        string ruleCode,
        CalculationRuleDefinition rule,
        CancellationToken cancellationToken = default)
    {
        Validate(rule);

        if (!string.Equals(
                ruleCode,
                rule.RuleCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Az útvonalban és a szabályban szereplő kódnak egyeznie kell.");
        }

        var path = GetFilePath(ruleCode);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"A szabály nem található: {ruleCode}");
        }

        await WriteAsync(
            path,
            rule,
            overwrite: true,
            cancellationToken);
    }

    private async Task WriteAsync(
        string path,
        CalculationRuleDefinition rule,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    rule,
                    JsonOptions,
                    cancellationToken);

                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetFilePath(string ruleCode)
    {
        if (string.IsNullOrWhiteSpace(ruleCode) ||
            !Regex.IsMatch(ruleCode, "^[A-Z0-9_]{1,100}$"))
        {
            throw new ArgumentException(
                "A szabálykód csak nagybetűt, számot és aláhúzást tartalmazhat.");
        }

        return Path.Combine(_rulesDirectory, $"{ruleCode}.json");
    }

    private static void Validate(CalculationRuleDefinition rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        _ = new RuleFileStorageServiceCodeValidator(rule.RuleCode);

        if (string.IsNullOrWhiteSpace(rule.Name) ||
            rule.Name.Length > 200)
        {
            throw new ArgumentException(
                "A szabály neve nem lehet üres, és legfeljebb 200 karakter lehet.");
        }

        if (rule.Version <= 0)
        {
            throw new ArgumentException(
                "A szabály verziószámának pozitívnak kell lennie.");
        }
    }

    private sealed class RuleFileStorageServiceCodeValidator
    {
        public RuleFileStorageServiceCodeValidator(string ruleCode)
        {
            if (string.IsNullOrWhiteSpace(ruleCode) ||
                !Regex.IsMatch(ruleCode, "^[A-Z0-9_]{1,100}$"))
            {
                throw new ArgumentException(
                    "A szabálykód csak nagybetűt, számot és aláhúzást tartalmazhat.");
            }
        }
    }
}
