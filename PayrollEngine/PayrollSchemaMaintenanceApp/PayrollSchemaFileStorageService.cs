using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using PayrollEngine.CalculationRuleMaintenanceApp;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;
using PayrollEngine.PayrollSchemaMaintenanceApp.Models;


namespace PayrollEngine.PayrollSchemaMaintenanceApp;

public sealed class PayrollSchemaFileStorageService
{
    private readonly string _schemasDirectory;
    private readonly RuleFileStorageService _rules;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public PayrollSchemaFileStorageService(
        IWebHostEnvironment environment,
        RuleFileStorageService rules)
    {
        _schemasDirectory = Path.Combine(
            environment.ContentRootPath,
            "PayrollSchemaMaintenanceApp",
            "Schemas");

        _rules = rules;
    }

    public async Task<IReadOnlyList<PayrollSchemaDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_schemasDirectory);

        var schemas = new List<PayrollSchemaDefinition>();

        foreach (var filePath in Directory.EnumerateFiles(
                     _schemasDirectory,
                     "*.json"))
        {
            await using var stream = File.OpenRead(filePath);

            var schema =
                await JsonSerializer.DeserializeAsync<PayrollSchemaDefinition>(
                    stream,
                    JsonOptions,
                    cancellationToken);

            if (schema is not null)
            {
                schemas.Add(schema);
            }
        }

        return schemas
            .OrderBy(schema => schema.Name)
            .ToArray();
    }

    public async Task<PayrollSchemaDefinition?> GetByCodeAsync(
        string schemaCode,
        CancellationToken cancellationToken = default)
    {
        ValidateSchemaCode(schemaCode);

        var filePath = GetFilePath(schemaCode);

        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);

        return await JsonSerializer.DeserializeAsync<PayrollSchemaDefinition>(
            stream,
            JsonOptions,
            cancellationToken);
    }

    public async Task CreateAsync(
        PayrollSchemaDefinition schema,
        CancellationToken cancellationToken = default)
    {
        ValidateSchemaCode(schema.SchemaCode);

        var existingSchemas = await GetAllAsync(cancellationToken);

        if (existingSchemas.Any(existing =>
                string.Equals(
                    existing.SchemaCode,
                    schema.SchemaCode,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Már létezik ilyen séma: {schema.SchemaCode}");
        }

        await ResolveRuleNamesAndCheckExistenceAsync(
            schema,
            cancellationToken);

        await WriteAsync(
            GetFilePath(schema.SchemaCode),
            schema,
            cancellationToken);
    }

    public async Task UpdateAsync(
        string schemaCode,
        PayrollSchemaDefinition schema,
        CancellationToken cancellationToken = default)
    {
        ValidateSchemaCode(schemaCode);
        ValidateSchemaCode(schema.SchemaCode);

        if (!string.Equals(
                schemaCode,
                schema.SchemaCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A séma kódja szerkesztéskor nem módosítható.",
                nameof(schema));
        }

        var filePath = GetFilePath(schemaCode);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"A séma nem található: {schemaCode}");
        }

        schema.SchemaCode = schemaCode;

        await ResolveRuleNamesAndCheckExistenceAsync(
            schema,
            cancellationToken);

        await WriteAsync(filePath, schema, cancellationToken);
    }

    public async Task<PayrollSchemaDefinition?> DeleteAsync(
        string schemaCode,
        CancellationToken cancellationToken = default)
    {
        ValidateSchemaCode(schemaCode);

        var filePath = GetFilePath(schemaCode);

        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);

        var schema =
            await JsonSerializer.DeserializeAsync<PayrollSchemaDefinition>(
                stream,
                JsonOptions,
                cancellationToken);

        File.Delete(filePath);

        return schema;
    }

    private async Task ResolveRuleNamesAndCheckExistenceAsync(
        PayrollSchemaDefinition schema,
        CancellationToken cancellationToken)
    {
        schema.Blocks ??= [];

        var availableRules = await _rules.GetAllAsync(cancellationToken);

        foreach (var block in schema.Blocks)
        {
            block.Rules ??= [];

            foreach (var ruleReference in block.Rules)
            {
                var rule = availableRules.FirstOrDefault(existing =>
                    string.Equals(
                        existing.RuleCode,
                        ruleReference.RuleCode,
                        StringComparison.OrdinalIgnoreCase));

                if (rule is null)
                {
                    throw new ArgumentException(
                        $"A hivatkozott szabály nem található: " +
                        $"{ruleReference.RuleCode}");
                }

                if (string.Equals(
                        rule.RuleType,
                        "callable",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"A segédszabály önállóan nem adható sémához: " +
                        $"{rule.RuleCode}");
                }

                // A név mindig a szabály JSON-fájljából származik.
                ruleReference.RuleName = rule.Name;
            }
        }
    }

    private async Task WriteAsync(
        string filePath,
        PayrollSchemaDefinition schema,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_schemasDirectory);

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(schema, JsonOptions);

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                cancellationToken);

            File.Move(
                temporaryPath,
                filePath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetFilePath(string schemaCode)
        => Path.Combine(_schemasDirectory, $"{schemaCode}.json");

    private static void ValidateSchemaCode(string? schemaCode)
    {
        if (string.IsNullOrWhiteSpace(schemaCode) ||
            schemaCode.Length > 100 ||
            schemaCode.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '_' or '-')))
        {
            throw new ArgumentException(
                "A sémakód csak betűt, számot, aláhúzást és kötőjelet " +
                "tartalmazhat, és legfeljebb 100 karakter lehet.",
                nameof(schemaCode));
        }
    }
}
