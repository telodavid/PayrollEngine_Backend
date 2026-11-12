using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportDataCatalogService
{
    private readonly string _catalogPath;
    private readonly ReportDatabaseCatalogBuilder _databaseCatalogBuilder;
    private readonly ReportDataCatalogMerger _catalogMerger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly Regex IdentifierPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant);

    public ReportDataCatalogService(
        IWebHostEnvironment environment,
        ReportDatabaseCatalogBuilder databaseCatalogBuilder,
        ReportDataCatalogMerger catalogMerger)
    {
        _catalogPath = Path.Combine(
            environment.ContentRootPath,
            "ReportBuilderApp",
            "Catalog",
            "report-data-sources.json");

        _databaseCatalogBuilder = databaseCatalogBuilder;
        _catalogMerger = catalogMerger;
    }

    public async Task<ReportDataCatalogDefinition> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var configured = await LoadConfiguredCatalogAsync(
            cancellationToken);

        ValidateCatalog(configured);

        var generated = await _databaseCatalogBuilder.BuildAsync(
            cancellationToken);

        ValidateCatalog(generated);

        var catalog = _catalogMerger.Merge(configured, generated);

        ValidateCatalog(catalog);

        return catalog;
    }

    public async Task<ReportDataSourceDefinition> GetSourceAsync(
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException(
                "Az adatforrás kódjának megadása kötelező.",
                nameof(sourceCode));
        }

        var catalog = await GetCatalogAsync(cancellationToken);

        return catalog.Sources.FirstOrDefault(source =>
                   Same(source.Code, sourceCode))
               ?? throw new KeyNotFoundException(
                   $"A riport-adatforrás nem található: {sourceCode}");
    }

    private async Task<ReportDataCatalogDefinition>
        LoadConfiguredCatalogAsync(
            CancellationToken cancellationToken)
    {
        if (!File.Exists(_catalogPath))
        {
            throw new FileNotFoundException(
                "A riport-adatforrások katalógusa nem található.",
                _catalogPath);
        }

        await using var stream = new FileStream(
            _catalogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        try
        {
            return await JsonSerializer
                       .DeserializeAsync<ReportDataCatalogDefinition>(
                           stream,
                           JsonOptions,
                           cancellationToken)
                   ?? throw new InvalidOperationException(
                       "A riport-adatforrások katalógusa üres.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "A riport-adatforrások katalógusa hibás JSON-t tartalmaz.",
                exception);
        }
    }

    private static void ValidateCatalog(
        ReportDataCatalogDefinition catalog)
    {
        if (catalog.Version <= 0)
        {
            throw new InvalidOperationException(
                "Az adatkatalógus verziójának pozitívnak kell lennie.");
        }

        if (catalog.Sources is null || catalog.Sources.Count == 0)
        {
            throw new InvalidOperationException(
                "Az adatkatalógus nem tartalmaz adatforrásokat.");
        }

        var codes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in catalog.Sources)
        {
            if (source is null)
            {
                throw new InvalidOperationException(
                    "Az adatkatalógus null adatforrást tartalmaz.");
            }

            ValidateIdentifier(source.Code, "Adatforráskód");

            if (!codes.Add(source.Code))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő adatforráskód: {source.Code}");
            }

            ValidateSource(source);
        }
    }

    private static void ValidateSource(
        ReportDataSourceDefinition source)
    {
        if (string.IsNullOrWhiteSpace(source.DisplayName))
        {
            throw new InvalidOperationException(
                $"Hiányzó adatforrás-megnevezés: {source.Code}");
        }

        ValidateEntity(source.Root, source.Code);

        if (source.Fields is null ||
            source.Fields.Count == 0 ||
            source.Relations is null ||
            source.Parameters is null ||
            source.Filters is null)
        {
            throw new InvalidOperationException(
                $"Hiányzó vagy érvénytelen adatforráslisták: {source.Code}");
        }

        var aliases = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            source.Root.Alias
        };

        var relations =
            new Dictionary<string, ReportDataRelationDefinition>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var relation in source.Relations)
        {
            if (relation is null)
            {
                throw new InvalidOperationException(
                    $"Null kapcsolat az adatforrásban: {source.Code}");
            }

            ValidateIdentifier(relation.Code, "Kapcsolatkód");
            ValidateEntity(relation.Target, relation.Code);

            if (!relations.TryAdd(relation.Code, relation))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő kapcsolatkód: {source.Code}/{relation.Code}");
            }

            if (!aliases.Add(relation.Target.Alias))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő táblaalias: {source.Code}/{relation.Target.Alias}");
            }

            if (!IsOneOf(relation.JoinType, "LEFT", "INNER"))
            {
                throw new InvalidOperationException(
                    $"Nem támogatott kapcsolástípus: {relation.JoinType}");
            }

            if (!IsOneOf(
                    relation.Cardinality,
                    "ONE_TO_ONE",
                    "ONE_TO_MANY",
                    "MANY_TO_ONE"))
            {
                throw new InvalidOperationException(
                    $"Nem támogatott kapcsolati számosság: {relation.Cardinality}");
            }
        }

        foreach (var relation in source.Relations)
        {
            ValidateRelation(source, relation, relations);
        }

        var references = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var field in source.Fields)
        {
            if (field is null ||
                string.IsNullOrWhiteSpace(field.Reference))
            {
                throw new InvalidOperationException(
                    $"Hiányzó mezőhivatkozás: {source.Code}");
            }

            if (!references.Add(field.Reference))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő mezőhivatkozás: {source.Code}/{field.Reference}");
            }

            if (string.IsNullOrWhiteSpace(field.DisplayName) ||
                string.IsNullOrWhiteSpace(field.Type))
            {
                throw new InvalidOperationException(
                    $"Hiányzó mezőmegnevezés vagy adattípus: {field.Reference}");
            }

            var expectedAlias = source.Root.Alias;

            if (field.RequiredRelationCode is not null)
            {
                if (!relations.TryGetValue(
                        field.RequiredRelationCode,
                        out var relation))
                {
                    throw new InvalidOperationException(
                        $"Ismeretlen mezőkapcsolat: " +
                        $"{field.Reference}/{field.RequiredRelationCode}");
                }

                expectedAlias = relation.Target.Alias;
            }

            if (!Same(field.EntityAlias, expectedAlias))
            {
                throw new InvalidOperationException(
                    $"A mező táblaaliasa nem egyezik a kijelölt forrással: " +
                    field.Reference);
            }

            if (field.DynamicField is null)
            {
                ValidateIdentifier(field.Column, "Mezőoszlop");
            }
            else
            {
                ValidateDynamicField(field);
            }
        }

        ValidateValidity(source.Validity, references);

        foreach (var relation in source.Relations)
        {
            ValidateValidity(relation.Validity, references);
        }

        ValidateReference(source.EmployeeIdField, references);
        ValidateReference(source.CompanyIdField, references);
    }

    private static void ValidateRelation(
        ReportDataSourceDefinition source,
        ReportDataRelationDefinition relation,
        IReadOnlyDictionary<string, ReportDataRelationDefinition> relations)
    {
        var visited = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            relation.Code
        };

        var current = relation;

        while (current.ParentRelationCode is not null)
        {
            var parentCode = current.ParentRelationCode;

            if (!visited.Add(parentCode))
            {
                throw new InvalidOperationException(
                    $"Körkörös adatkapcsolat: {source.Code}/{relation.Code}");
            }

            if (!relations.TryGetValue(parentCode, out var parent))
            {
                throw new InvalidOperationException(
                    $"Ismeretlen szülőkapcsolat: {source.Code}/{parentCode}");
            }

            current = parent;
        }

        var parentAlias = relation.ParentRelationCode is null
            ? source.Root.Alias
            : relations[relation.ParentRelationCode].Target.Alias;

        if (relation.Conditions is null ||
            relation.Conditions.Count == 0)
        {
            throw new InvalidOperationException(
                $"Hiányzó kapcsolási feltételek: {relation.Code}");
        }

        foreach (var condition in relation.Conditions)
        {
            if (condition is null)
            {
                throw new InvalidOperationException(
                    $"Null kapcsolási feltétel: {relation.Code}");
            }

            ValidateIdentifier(condition.LeftColumn, "Bal oldali oszlop");
            ValidateIdentifier(condition.RightColumn, "Jobb oldali oszlop");

            if (!Same(condition.LeftEntityAlias, parentAlias) ||
                !Same(
                    condition.RightEntityAlias,
                    relation.Target.Alias))
            {
                throw new InvalidOperationException(
                    $"A kapcsolási feltételnek a szülőt és a céltáblát " +
                    $"kell összekötnie: {relation.Code}");
            }
        }
    }

    private static void ValidateDynamicField(
        ReportDataFieldDefinition field)
    {
        var definition = field.DynamicField!;

        if (definition.MasterDataFieldId <= 0)
        {
            throw new InvalidOperationException(
                $"Érvénytelen dinamikusmező-azonosító: {field.Reference}");
        }

        if (!Same(definition.OwnerEntityAlias, field.EntityAlias))
        {
            throw new InvalidOperationException(
                $"Érvénytelen dinamikusmező-tulajdonos: {field.Reference}");
        }

        ValidateEntity(definition.ValueEntity, field.Reference);

        ValidateIdentifier(
            definition.OwnerIdColumn,
            "Tulajdonos kulcsoszlopa");

        ValidateIdentifier(
            definition.OwnerForeignKeyColumn,
            "Értéktábla tulajdonososzlopa");

        ValidateIdentifier(
            definition.FieldIdColumn,
            "Dinamikusmező-azonosító oszlopa");

        ValidateIdentifier(
            definition.ValueColumn,
            "Dinamikus érték oszlopa");

        if (definition.ValidFromColumn is not null)
        {
            ValidateIdentifier(
                definition.ValidFromColumn,
                "Érvényesség kezdőoszlopa");
        }

        if (definition.ValidToColumn is not null)
        {
            ValidateIdentifier(
                definition.ValidToColumn,
                "Érvényesség záróoszlopa");
        }
    }

    private static void ValidateValidity(
        ReportDataValidityDefinition? validity,
        IReadOnlySet<string> references)
    {
        if (validity is null)
        {
            return;
        }

        ValidateReference(validity.DateField, references);
        ValidateReference(validity.ValidFromField, references);
        ValidateReference(validity.ValidToField, references);
    }

    private static void ValidateReference(
        string? reference,
        IReadOnlySet<string> references)
    {
        if (reference is not null && !references.Contains(reference))
        {
            throw new InvalidOperationException(
                $"Ismeretlen mezőhivatkozás: {reference}");
        }
    }

    private static void ValidateEntity(
        ReportDataEntityDefinition? entity,
        string owner)
    {
        if (entity is null)
        {
            throw new InvalidOperationException(
                $"Hiányzó tábladefiníció: {owner}");
        }

        ValidateIdentifier(entity.Schema, "Séma");
        ValidateIdentifier(entity.Entity, "Tábla");
        ValidateIdentifier(entity.Alias, "Táblaalias");
    }

    private static void ValidateIdentifier(
        string? value,
        string label)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 128 ||
            !IdentifierPattern.IsMatch(value))
        {
            throw new InvalidOperationException(
                $"{label}: érvénytelen azonosító: {value}");
        }
    }

    private static bool IsOneOf(
        string? value,
        params string[] allowedValues)
    {
        return allowedValues.Any(allowed => Same(value, allowed));
    }

    private static bool Same(string? first, string? second)
    {
        return string.Equals(
            first,
            second,
            StringComparison.OrdinalIgnoreCase);
    }
}