using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportDataCatalogMerger
{
    public ReportDataCatalogDefinition Merge(
        ReportDataCatalogDefinition configured,
        ReportDataCatalogDefinition generated)
    {
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(generated);

        var generatedSources = generated.Sources.ToDictionary(
            source => source.Code,
            StringComparer.OrdinalIgnoreCase);

        var configuredCodes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var result = new ReportDataCatalogDefinition
        {
            Version = Math.Max(
                configured.Version,
                generated.Version)
        };

        foreach (var source in configured.Sources)
        {
            if (!configuredCodes.Add(source.Code))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő konfigurált adatforráskód: {source.Code}");
            }

            if (!generatedSources.TryGetValue(
                    source.Code,
                    out var generatedSource))
            {
                throw new InvalidOperationException(
                    "A konfigurált adatforráshoz nem található " +
                    $"engedélyezett adatbázistábla: {source.Code}");
            }

            MergeSource(source, generatedSource);
            result.Sources.Add(source);
        }

        foreach (var source in generated.Sources)
        {
            if (!configuredCodes.Contains(source.Code))
                result.Sources.Add(source);
        }

        return result;
    }

    private static void MergeSource(
        ReportDataSourceDefinition configured,
        ReportDataSourceDefinition generated)
    {
        if (!Same(
                configured.Root.Schema,
                generated.Root.Schema) ||
            !Same(
                configured.Root.Entity,
                generated.Root.Entity))
        {
            throw new InvalidOperationException(
                $"Eltérő kiinduló tábla az adatforrásban: {configured.Code}");
        }

        var aliases = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            [generated.Root.Alias] = configured.Root.Alias
        };

        var relationCodes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var relationPairs =
            new List<(
                ReportDataRelationDefinition Generated,
                ReportDataRelationDefinition Target)>();

        foreach (var generatedRelation in generated.Relations)
        {
            var matches = configured.Relations
                .Where(relation => EquivalentRelation(
                    relation,
                    generatedRelation))
                .ToArray();

            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    "Több konfigurált kapcsolat illeszkedik ugyanahhoz " +
                    "az adatbázis-kapcsolathoz: " +
                    $"{configured.Code}/{generatedRelation.Code}");
            }

            var target = matches.SingleOrDefault();

            if (target is null)
            {
                if (configured.Relations.Any(relation =>
                        Same(relation.Code, generatedRelation.Code) ||
                        Same(
                            relation.Target.Alias,
                            generatedRelation.Target.Alias)) ||
                    Same(
                        configured.Root.Alias,
                        generatedRelation.Target.Alias))
                {
                    throw new InvalidOperationException(
                        "Ütköző generált kapcsolat: " +
                        $"{configured.Code}/{generatedRelation.Code}");
                }

                target = new ReportDataRelationDefinition
                {
                    Code = generatedRelation.Code,
                    DisplayName = generatedRelation.DisplayName,
                    Target = new ReportDataEntityDefinition
                    {
                        Schema = generatedRelation.Target.Schema,
                        Entity = generatedRelation.Target.Entity,
                        Alias = generatedRelation.Target.Alias
                    },
                    JoinType = generatedRelation.JoinType,
                    Cardinality = generatedRelation.Cardinality,
                    Conditions = generatedRelation.Conditions
                        .Select(condition =>
                            new ReportDataJoinConditionDefinition
                            {
                                LeftEntityAlias = configured.Root.Alias,
                                LeftColumn = condition.LeftColumn,
                                RightEntityAlias =
                                    generatedRelation.Target.Alias,
                                RightColumn = condition.RightColumn
                            })
                        .ToList()
                };

                configured.Relations.Add(target);
            }

            aliases.Add(
                generatedRelation.Target.Alias,
                target.Target.Alias);

            relationCodes.Add(
                generatedRelation.Code,
                target.Code);

            relationPairs.Add((generatedRelation, target));
        }

        var referenceMap = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var generatedField in generated.Fields)
        {
            if (!aliases.TryGetValue(
                    generatedField.EntityAlias,
                    out var alias))
            {
                throw new InvalidOperationException(
                    "Nem feloldható generált táblaalias: " +
                    generatedField.EntityAlias);
            }

            string? relationCode = null;

            if (generatedField.RequiredRelationCode is not null)
            {
                if (!relationCodes.TryGetValue(
                        generatedField.RequiredRelationCode,
                        out relationCode))
                {
                    throw new InvalidOperationException(
                        "Nem feloldható generált adatkapcsolat: " +
                        generatedField.RequiredRelationCode);
                }
            }

            var dynamicField = MapDynamicField(
                generatedField.DynamicField,
                aliases);

            var matches = configured.Fields
                .Where(existing =>
                    Same(existing.EntityAlias, alias) &&
                    Same(existing.Column, generatedField.Column) &&
                    Same(
                        existing.RequiredRelationCode,
                        relationCode) &&
                    EquivalentDynamicField(
                        existing.DynamicField,
                        dynamicField))
                .ToArray();

            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    "Több konfigurált mező hivatkozik ugyanarra az adatra: " +
                    $"{configured.Code}/{generatedField.Reference}");
            }

            var target = matches.SingleOrDefault();

            if (target is null)
            {
                if (configured.Fields.Any(existing =>
                        Same(
                            existing.Reference,
                            generatedField.Reference)))
                {
                    throw new InvalidOperationException(
                        "Ütköző mezőhivatkozás: " +
                        $"{configured.Code}/{generatedField.Reference}");
                }

                target = new ReportDataFieldDefinition
                {
                    Reference = generatedField.Reference,
                    DisplayName = generatedField.DisplayName,
                    EntityAlias = alias,
                    Column = generatedField.Column,
                    Type = generatedField.Type,
                    IsNullable = generatedField.IsNullable,
                    CanFilter = generatedField.CanFilter,
                    CanGroup = generatedField.CanGroup,
                    CanSort = generatedField.CanSort,
                    RequiredRelationCode = relationCode,
                    DynamicField = dynamicField
                };

                configured.Fields.Add(target);
            }
            else
            {
                target.CanFilter &= generatedField.CanFilter;
                target.CanGroup &= generatedField.CanGroup;
                target.CanSort &= generatedField.CanSort;

                if (dynamicField is not null)
                {
                    target.DynamicField = dynamicField;
                    target.Type = generatedField.Type;
                    target.IsNullable = generatedField.IsNullable;
                }
            }

            referenceMap.Add(
                generatedField.Reference,
                target.Reference);
        }

        configured.EmployeeIdField ??= MapReference(
            generated.EmployeeIdField,
            referenceMap);

        configured.CompanyIdField ??= MapReference(
            generated.CompanyIdField,
            referenceMap);

        configured.Validity ??= MapValidity(
            generated.Validity,
            referenceMap);

        foreach (var pair in relationPairs)
        {
            pair.Target.Validity ??= MapValidity(
                pair.Generated.Validity,
                referenceMap);
        }
    }

    private static ReportDynamicFieldDefinition? MapDynamicField(
        ReportDynamicFieldDefinition? definition,
        IReadOnlyDictionary<string, string> aliases)
    {
        if (definition is null)
            return null;

        if (!aliases.TryGetValue(
                definition.OwnerEntityAlias,
                out var ownerAlias))
        {
            throw new InvalidOperationException(
                "Nem feloldható a dinamikus mező tulajdonosának aliasa: " +
                definition.OwnerEntityAlias);
        }

        return new ReportDynamicFieldDefinition
        {
            MasterDataFieldId = definition.MasterDataFieldId,
            OwnerEntityAlias = ownerAlias,
            OwnerIdColumn = definition.OwnerIdColumn,
            ValueEntity = new ReportDataEntityDefinition
            {
                Schema = definition.ValueEntity.Schema,
                Entity = definition.ValueEntity.Entity,
                Alias = definition.ValueEntity.Alias
            },
            OwnerForeignKeyColumn =
                definition.OwnerForeignKeyColumn,
            FieldIdColumn = definition.FieldIdColumn,
            ValueColumn = definition.ValueColumn,
            ValidFromColumn = definition.ValidFromColumn,
            ValidToColumn = definition.ValidToColumn
        };
    }

    private static bool EquivalentDynamicField(
        ReportDynamicFieldDefinition? configured,
        ReportDynamicFieldDefinition? generated)
    {
        if (configured is null || generated is null)
            return configured is null && generated is null;

        return
            configured.MasterDataFieldId ==
                generated.MasterDataFieldId &&
            Same(
                configured.OwnerEntityAlias,
                generated.OwnerEntityAlias) &&
            Same(
                configured.OwnerIdColumn,
                generated.OwnerIdColumn) &&
            Same(
                configured.ValueEntity.Schema,
                generated.ValueEntity.Schema) &&
            Same(
                configured.ValueEntity.Entity,
                generated.ValueEntity.Entity) &&
            Same(
                configured.OwnerForeignKeyColumn,
                generated.OwnerForeignKeyColumn) &&
            Same(
                configured.FieldIdColumn,
                generated.FieldIdColumn) &&
            Same(
                configured.ValueColumn,
                generated.ValueColumn) &&
            Same(
                configured.ValidFromColumn,
                generated.ValidFromColumn) &&
            Same(
                configured.ValidToColumn,
                generated.ValidToColumn);
    }

    private static bool EquivalentRelation(
        ReportDataRelationDefinition configured,
        ReportDataRelationDefinition generated)
    {
        if (configured.ParentRelationCode is not null ||
            generated.ParentRelationCode is not null ||
            !Same(
                configured.Target.Schema,
                generated.Target.Schema) ||
            !Same(
                configured.Target.Entity,
                generated.Target.Entity) ||
            configured.Conditions.Count != generated.Conditions.Count)
        {
            return false;
        }

        var configuredConditions = configured.Conditions
            .Select(condition =>
                $"{condition.LeftColumn}|{condition.RightColumn}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var generatedConditions = generated.Conditions
            .Select(condition =>
                $"{condition.LeftColumn}|{condition.RightColumn}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return configuredConditions.SetEquals(generatedConditions);
    }

    private static ReportDataValidityDefinition? MapValidity(
        ReportDataValidityDefinition? validity,
        IReadOnlyDictionary<string, string> references)
    {
        if (validity is null)
            return null;

        return new ReportDataValidityDefinition
        {
            DateField = MapReference(
                validity.DateField,
                references),
            ValidFromField = MapReference(
                validity.ValidFromField,
                references),
            ValidToField = MapReference(
                validity.ValidToField,
                references)
        };
    }

    private static string? MapReference(
        string? reference,
        IReadOnlyDictionary<string, string> references)
    {
        if (reference is null)
            return null;

        if (!references.TryGetValue(reference, out var mapped))
        {
            throw new InvalidOperationException(
                $"Nem feloldható generált mezőhivatkozás: {reference}");
        }

        return mapped;
    }

    private static bool Same(
        string? first,
        string? second)
    {
        return string.Equals(
            first,
            second,
            StringComparison.OrdinalIgnoreCase);
    }
}