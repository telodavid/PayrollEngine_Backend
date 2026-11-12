using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public sealed class OperationCatalogService
{
    private readonly OperationCatalogDefinition _catalog;

    public OperationCatalogService(IWebHostEnvironment environment)
    {
        var path = Path.Combine(
            environment.ContentRootPath,
            "CalculationRuleMaintenanceApp",
            "Operations",
            "operations.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "A műveletkatalógus JSON-fájlja nem található.",
                path);
        }

        var json = File.ReadAllText(path);

        _catalog = JsonSerializer.Deserialize<OperationCatalogDefinition>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException(
                "A műveletkatalógus üres vagy hibás JSON.");

        ValidateCatalog(_catalog);
    }

    public OperationCatalogDefinition Catalog => _catalog;

    private static void ValidateCatalog(OperationCatalogDefinition catalog)
    {
        if (catalog.CatalogVersion <= 0)
        {
            throw new InvalidDataException(
                "A műveletkatalógus verziószáma érvénytelen.");
        }

        if (catalog.Operations.Count == 0)
        {
            throw new InvalidDataException(
                "A műveletkatalógus nem tartalmaz műveleteket.");
        }

        var duplicateCode = catalog.Operations
            .GroupBy(operation => operation.Code, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCode is not null)
        {
            throw new InvalidDataException(
                $"A műveletkód többször szerepel: {duplicateCode.Key}");
        }

        foreach (var operation in catalog.Operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Code) ||
                string.IsNullOrWhiteSpace(operation.Category) ||
                string.IsNullOrWhiteSpace(operation.Kind) ||
                string.IsNullOrWhiteSpace(operation.DisplayName))
            {
                throw new InvalidDataException(
                    "Minden műveletnek kell kód, kategória, típus és megjelenített név.");
            }

            var duplicateInput = operation.Inputs
                .GroupBy(input => input.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateInput is not null)
            {
                throw new InvalidDataException(
                    $"A(z) {operation.Code} műveletben ismétlődik a bemenet neve: {duplicateInput.Key}");
            }

            foreach (var input in operation.Inputs)
            {
                if (string.IsNullOrWhiteSpace(input.Name) ||
                    string.IsNullOrWhiteSpace(input.Type))
                {
                    throw new InvalidDataException(
                        $"A(z) {operation.Code} művelet egyik bemenetéből hiányzik a név vagy a típus.");
                }

                if (input.SameTypeAs is not null &&
                    !operation.Inputs.Any(candidate =>
                        string.Equals(
                            candidate.Name,
                            input.SameTypeAs,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException(
                        $"A(z) {operation.Code} művelet {input.Name} bemenete nem létező bemenetre hivatkozik: {input.SameTypeAs}");
                }
            }
        }
    }
}