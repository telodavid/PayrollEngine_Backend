using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportDatabaseCatalogBuilder(
    ReportDatabaseMetadataRepository metadataRepository)
{
    public async Task<ReportDataCatalogDefinition> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var metadata = await metadataRepository.LoadAsync(
            cancellationToken);

        var dynamicFields = await metadataRepository.LoadDynamicFieldsAsync(
            cancellationToken);

        var tables = metadata.Columns
            .GroupBy(item => TableKey(
                item.SchemaName,
                item.TableName))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.ColumnOrder)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var foreignKeys = metadata.ForeignKeyColumns
            .Where(item =>
                !item.IsDisabled &&
                !item.IsNotTrusted)
            .GroupBy(item => item.ForeignKeyId)
            .Select(group => group
                .OrderBy(item => item.ColumnOrder)
                .ToArray())
            .ToArray();

        var catalog = new ReportDataCatalogDefinition
        {
            Version = 2
        };

        var sourceCodes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables.Values
                     .OrderBy(items => items[0].TableName))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var first = table[0];
            var source = CreateSource(table);

            if (!sourceCodes.Add(source.Code))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő generált adatforráskód: {source.Code}");
            }

            foreach (var foreignKey in foreignKeys)
            {
                var key = foreignKey[0];

                var isParent = SameTable(
                    first.SchemaName,
                    first.TableName,
                    key.ParentSchemaName,
                    key.ParentTableName);

                var isReferenced = SameTable(
                    first.SchemaName,
                    first.TableName,
                    key.ReferencedSchemaName,
                    key.ReferencedTableName);

                if (isParent)
                {
                    AddRelation(
                        source,
                        foreignKey,
                        tables,
                        reverse: false);
                }

                if (isReferenced)
                {
                    AddRelation(
                        source,
                        foreignKey,
                        tables,
                        reverse: true);
                }
            }

            ResolveScopeFields(source);

            AddDynamicFields(
                source,
                dynamicFields,
                tables);

            catalog.Sources.Add(source);
        }

        return catalog;
    }

    private static ReportDataSourceDefinition CreateSource(
        IReadOnlyList<ReportDatabaseColumn> columns)
    {
        var first = columns[0];

        var source = new ReportDataSourceDefinition
        {
            Code = ToSourceCode(first.TableName),
            DisplayName = first.TableDisplayName,
            Root = new ReportDataEntityDefinition
            {
                Schema = first.SchemaName,
                Entity = first.TableName,
                Alias = "root"
            }
        };

        foreach (var column in columns)
        {
            source.Fields.Add(CreateField(
                column,
                "root",
                $"{first.SchemaName}.{first.TableName}.{column.ColumnName}",
                null));
        }

        source.EmployeeIdField = FindReference(
            source.Fields,
            "EmployeeId");

        source.CompanyIdField = FindReference(
            source.Fields,
            "CompanyId");

        source.Validity = CreateValidity(source.Fields);

        return source;
    }

    private static void AddRelation(
        ReportDataSourceDefinition source,
        IReadOnlyList<ReportDatabaseForeignKeyColumn> foreignKey,
        IReadOnlyDictionary<string, ReportDatabaseColumn[]> tables,
        bool reverse)
    {
        var first = foreignKey[0];

        var targetSchema = reverse
            ? first.ParentSchemaName
            : first.ReferencedSchemaName;

        var targetTable = reverse
            ? first.ParentTableName
            : first.ReferencedTableName;

        if (!tables.TryGetValue(
                TableKey(targetSchema, targetTable),
                out var targetColumns))
        {
            return;
        }

        var relationCode = CreateRelationCode(
            foreignKey,
            reverse);

        var alias = relationCode.ToLowerInvariant();

        var relation = new ReportDataRelationDefinition
        {
            Code = relationCode,
            DisplayName =
                $"{targetColumns[0].TableDisplayName} " +
                $"({string.Join(", ", foreignKey.Select(
                    item => item.ParentColumnName))})",
            Target = new ReportDataEntityDefinition
            {
                Schema = targetSchema,
                Entity = targetTable,
                Alias = alias
            },
            JoinType = "LEFT",
            Cardinality = reverse
                ? "ONE_TO_MANY"
                : "MANY_TO_ONE"
        };

        foreach (var item in foreignKey)
        {
            relation.Conditions.Add(
                new ReportDataJoinConditionDefinition
                {
                    LeftEntityAlias = source.Root.Alias,
                    LeftColumn = reverse
                        ? item.ReferencedColumnName
                        : item.ParentColumnName,
                    RightEntityAlias = alias,
                    RightColumn = reverse
                        ? item.ParentColumnName
                        : item.ReferencedColumnName
                });
        }

        var relatedFields = targetColumns
            .Select(item => CreateField(
                item,
                alias,
                $"{relationCode}.{item.ColumnName}",
                relationCode))
            .ToList();

        relation.Validity = CreateValidity(relatedFields);

        source.Relations.Add(relation);
        source.Fields.AddRange(relatedFields);
    }

    private static void AddDynamicFields(
        ReportDataSourceDefinition source,
        IReadOnlyList<ReportDynamicFieldMetadata> definitions,
        IReadOnlyDictionary<string, ReportDatabaseColumn[]> tables)
    {
        var references = source.Fields
            .Select(item => item.Reference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        AddDynamicFieldsForEntity(
            source,
            source.Root,
            null,
            definitions,
            tables,
            references);

        foreach (var relation in source.Relations)
        {
            AddDynamicFieldsForEntity(
                source,
                relation.Target,
                relation.Code,
                definitions,
                tables,
                references);
        }
    }

    private static void AddDynamicFieldsForEntity(
        ReportDataSourceDefinition source,
        ReportDataEntityDefinition owner,
        string? relationCode,
        IReadOnlyList<ReportDynamicFieldMetadata> definitions,
        IReadOnlyDictionary<string, ReportDatabaseColumn[]> tables,
        HashSet<string> references)
    {
        var matchingDefinitions = definitions
            .Where(item => SameTable(
                owner.Schema,
                owner.Entity,
                item.OwnerSchema,
                item.OwnerEntity))
            .OrderBy(item => item.GroupName)
            .ThenBy(item => item.DisplayOrder)
            .ThenBy(item => item.DisplayName)
            .ThenBy(item => item.MasterDataFieldId);

        foreach (var definition in matchingDefinitions)
        {
            ValidateDynamicFieldMetadata(
                definition,
                tables);

            var fieldId = definition.MasterDataFieldId.ToString(
                CultureInfo.InvariantCulture);

            var reference = relationCode is null
                ? $"{owner.Schema}.{owner.Entity}.DynamicField.{fieldId}"
                : $"{relationCode}.DynamicField.{fieldId}";

            if (!references.Add(reference))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő dinamikus riportmező: {reference}");
            }

            var label =
                $"{definition.GroupName} / {definition.DisplayName}";

            if (relationCode is not null)
            {
                var relation = source.Relations.First(item =>
                    Same(item.Code, relationCode));

                label = $"{relation.DisplayName} / {label}";
            }

            source.Fields.Add(new ReportDataFieldDefinition
            {
                Reference = reference,
                DisplayName = label,
                EntityAlias = owner.Alias,
                Column = definition.ValueColumn,
                Type = ResolveDynamicFieldType(
                    definition.FieldTypeCode),
                IsNullable = true,
                CanFilter = true,
                CanGroup = true,
                CanSort = true,
                RequiredRelationCode = relationCode,
                DynamicField = new ReportDynamicFieldDefinition
                {
                    MasterDataFieldId = definition.MasterDataFieldId,
                    OwnerEntityAlias = owner.Alias,
                    OwnerIdColumn = definition.OwnerIdColumn,
                    ValueEntity = new ReportDataEntityDefinition
                    {
                        Schema = definition.ValueSchema,
                        Entity = definition.ValueEntity,
                        Alias = $"dynamic_value_{fieldId}"
                    },
                    OwnerForeignKeyColumn =
                        definition.OwnerForeignKeyColumn,
                    FieldIdColumn = definition.FieldIdColumn,
                    ValueColumn = definition.ValueColumn,
                    ValidFromColumn = definition.ValidFromColumn,
                    ValidToColumn = definition.ValidToColumn
                }
            });
        }
    }

    private static void ValidateDynamicFieldMetadata(
        ReportDynamicFieldMetadata definition,
        IReadOnlyDictionary<string, ReportDatabaseColumn[]> tables)
    {
        if (definition.MasterDataFieldId <= 0)
        {
            throw new InvalidOperationException(
                "A dinamikus mező azonosítója érvénytelen.");
        }

        if (!tables.TryGetValue(
                TableKey(
                    definition.OwnerSchema,
                    definition.OwnerEntity),
                out var ownerColumns))
        {
            throw new InvalidOperationException(
                "A dinamikus mező tulajdonosának táblája " +
                $"nem szerepel a katalógusban: {definition.DisplayName}");
        }

        if (!tables.TryGetValue(
                TableKey(
                    definition.ValueSchema,
                    definition.ValueEntity),
                out var valueColumns))
        {
            throw new InvalidOperationException(
                "A dinamikus mező értéktáblája " +
                $"nem szerepel a katalógusban: {definition.DisplayName}");
        }

        RequireColumn(
            ownerColumns,
            definition.OwnerIdColumn,
            definition.DisplayName);

        RequireColumn(
            valueColumns,
            definition.OwnerForeignKeyColumn,
            definition.DisplayName);

        RequireColumn(
            valueColumns,
            definition.FieldIdColumn,
            definition.DisplayName);

        RequireColumn(
            valueColumns,
            definition.ValueColumn,
            definition.DisplayName);

        RequireColumn(
            valueColumns,
            definition.OptionIdColumn,
            definition.DisplayName);

        if (!string.IsNullOrWhiteSpace(definition.ValidFromColumn))
        {
            RequireColumn(
                valueColumns,
                definition.ValidFromColumn,
                definition.DisplayName);
        }

        if (!string.IsNullOrWhiteSpace(definition.ValidToColumn))
        {
            RequireColumn(
                valueColumns,
                definition.ValidToColumn,
                definition.DisplayName);
        }
    }

    private static void RequireColumn(
        IReadOnlyList<ReportDatabaseColumn> columns,
        string columnName,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(columnName) ||
            !columns.Any(item => Same(
                item.ColumnName,
                columnName)))
        {
            throw new InvalidOperationException(
                $"Hiányzó dinamikusmező-oszlop: {columnName}; " +
                $"mező: {displayName}");
        }
    }

    private static string ResolveDynamicFieldType(
        string fieldTypeCode)
    {
        return fieldTypeCode.Trim().ToUpperInvariant() switch
        {
            "NUMBER" or "DECIMAL" or "NUMERIC" => "decimal",
            "INTEGER" or "INT" => "integer",
            "DATE" => "date",
            "DATETIME" => "datetime",
            "DATETIMEOFFSET" => "datetimeoffset",
            "TIME" => "time",
            "BOOLEAN" or "BOOL" => "boolean",
            "MONTH" => "month",
            "GUID" => "guid",
            _ => "text"
        };
    }

    private static void ResolveScopeFields(
        ReportDataSourceDefinition source)
    {
        source.EmployeeIdField ??= FindRelatedScopeReference(
            source,
            "EmployeeId");

        source.CompanyIdField ??= FindRelatedScopeReference(
            source,
            "CompanyId");
    }

    private static string? FindRelatedScopeReference(
        ReportDataSourceDefinition source,
        string columnName)
    {
        var eligibleRelationCodes = source.Relations
            .Where(item =>
                item.ParentRelationCode is null &&
                (Same(item.Cardinality, "MANY_TO_ONE") ||
                 Same(item.Cardinality, "ONE_TO_ONE")))
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = source.Fields
            .Where(item =>
                item.DynamicField is null &&
                item.RequiredRelationCode is not null &&
                eligibleRelationCodes.Contains(
                    item.RequiredRelationCode) &&
                Same(item.Column, columnName))
            .Select(item => item.Reference)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();

        return candidates.Length == 1
            ? candidates[0]
            : null;
    }

    private static ReportDataFieldDefinition CreateField(
        ReportDatabaseColumn column,
        string alias,
        string reference,
        string? relationCode)
    {
        var comparable =
            column.DataType is not ("binary" or "text") ||
            column.SqlType is
                "char" or "varchar" or "nchar" or "nvarchar";

        return new ReportDataFieldDefinition
        {
            Reference = reference,
            DisplayName = relationCode is null
                ? column.ColumnName
                : $"{column.TableDisplayName} – {column.ColumnName}",
            EntityAlias = alias,
            Column = column.ColumnName,
            Type = column.DataType,
            IsNullable =
                column.IsNullable ||
                relationCode is not null,
            CanFilter = comparable,
            CanGroup = comparable,
            CanSort = comparable,
            RequiredRelationCode = relationCode
        };
    }

    private static ReportDataValidityDefinition? CreateValidity(
        IReadOnlyList<ReportDataFieldDefinition> fields)
    {
        var validFrom = FindReference(fields, "ValidFrom");
        var validTo = FindReference(fields, "ValidTo");

        if (validFrom is not null)
        {
            return new ReportDataValidityDefinition
            {
                ValidFromField = validFrom,
                ValidToField = validTo
            };
        }

        var startDate = FindReference(fields, "StartDate");
        var endDate = FindReference(fields, "EndDate");

        if (startDate is not null && endDate is not null)
        {
            return new ReportDataValidityDefinition
            {
                ValidFromField = startDate,
                ValidToField = endDate
            };
        }

        var period = FindReference(fields, "Period");

        if (period is not null)
        {
            var periodField = fields.First(item =>
                Same(item.Reference, period));

            periodField.Type = "month";

            return new ReportDataValidityDefinition
            {
                DateField = period
            };
        }

        return null;
    }

    private static string? FindReference(
        IEnumerable<ReportDataFieldDefinition> fields,
        string columnName)
    {
        return fields.FirstOrDefault(item =>
            item.DynamicField is null &&
            Same(item.Column, columnName))?.Reference;
    }

    private static string CreateRelationCode(
        IReadOnlyList<ReportDatabaseForeignKeyColumn> columns,
        bool reverse)
    {
        var first = columns[0];

        var identity = string.Join(
            "|",
            columns.Select(item => string.Join(
                ".",
                item.ParentSchemaName,
                item.ParentTableName,
                item.ParentColumnName,
                item.ReferencedSchemaName,
                item.ReferencedTableName,
                item.ReferencedColumnName)));

        identity += reverse
            ? "|REVERSE"
            : "|FORWARD";

        var hash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(identity)));

        var targetTable = reverse
            ? first.ParentTableName
            : first.ReferencedTableName;

        var targetCode = ToSourceCode(targetTable);

        if (targetCode.Length > 80)
            targetCode = targetCode[..80];

        return $"AUTO_{targetCode}_{hash[..16]}";
    }

    private static string ToSourceCode(string tableName)
    {
        var separatedAcronyms = Regex.Replace(
            tableName,
            @"([A-Z]+)([A-Z][a-z])",
            "$1_$2");

        return Regex.Replace(
                separatedAcronyms,
                @"([a-z0-9])([A-Z])",
                "$1_$2")
            .ToUpperInvariant();
    }

    private static string TableKey(
        string schemaName,
        string tableName)
    {
        return $"{schemaName}.{tableName}";
    }

    private static bool SameTable(
        string firstSchema,
        string firstTable,
        string secondSchema,
        string secondTable)
    {
        return Same(firstSchema, secondSchema) &&
               Same(firstTable, secondTable);
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