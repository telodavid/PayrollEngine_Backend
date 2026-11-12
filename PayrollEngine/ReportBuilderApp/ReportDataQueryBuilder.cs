using System.Globalization;
using System.Text.Json;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportDataQueryBuilder(
    ReportParameterValueResolver parameterResolver)
{
    public ReportDataQueryDefinition Build(
        ReportDefinition report,
        ReportExportRequest request,
        ReportDataSourceDefinition source)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        if (!Same(report.DataSource, source.Code))
        {
            throw new InvalidOperationException(
                "A riport adatforrása nem egyezik a betöltött katalógussal.");
        }

        if (report.Fields is null || report.Fields.Count == 0)
        {
            throw new InvalidOperationException(
                "A riporthoz legalább egy kimeneti mezőt meg kell adni.");
        }

        if (report.Grouping is null ||
            report.Relations is null ||
            report.Filters is null ||
            report.Parameters is null ||
            report.DateFilter is null)
        {
            throw new InvalidOperationException(
                "A riport konfigurációja hiányos.");
        }

        var fields = source.Fields.ToDictionary(
            field => field.Reference,
            StringComparer.OrdinalIgnoreCase);

        var relations = source.Relations.ToDictionary(
            relation => relation.Code,
            StringComparer.OrdinalIgnoreCase);

        var query = new ReportDataQueryDefinition
        {
            Root = new ReportDataEntityDefinition
            {
                Schema = source.Root.Schema,
                Entity = source.Root.Entity,
                Alias = source.Root.Alias
            }
        };

        var selectedReferences = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var includedRelations = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        ReportDataFieldDefinition FindField(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) ||
                !fields.TryGetValue(reference, out var field))
            {
                throw new InvalidOperationException(
                    $"A mező nem található az adatforrásban: {reference}");
            }

            return field;
        }

        void IncludeRelation(string code)
        {
            if (includedRelations.Contains(code))
            {
                return;
            }

            if (!relations.TryGetValue(code, out var relation))
            {
                throw new InvalidOperationException(
                    $"Ismeretlen riportkapcsolat: {code}");
            }

            if (relation.ParentRelationCode is not null)
            {
                IncludeRelation(relation.ParentRelationCode);
            }

            query.Joins.Add(new ReportDataQueryJoin
            {
                Code = relation.Code,
                Target = new ReportDataEntityDefinition
                {
                    Schema = relation.Target.Schema,
                    Entity = relation.Target.Entity,
                    Alias = relation.Target.Alias
                },
                JoinType = relation.JoinType,
                Conditions = relation.Conditions
                    .Select(condition => new ReportDataJoinConditionDefinition
                    {
                        LeftEntityAlias = condition.LeftEntityAlias,
                        LeftColumn = condition.LeftColumn,
                        RightEntityAlias = condition.RightEntityAlias,
                        RightColumn = condition.RightColumn
                    })
                    .ToList()
            });

            includedRelations.Add(code);
        }

        void IncludeField(ReportDataFieldDefinition field)
        {
            if (field.RequiredRelationCode is not null)
            {
                IncludeRelation(field.RequiredRelationCode);
            }

            if (!selectedReferences.Add(field.Reference))
            {
                return;
            }

            query.Fields.Add(new ReportDataQueryField
            {
                Reference = field.Reference,
                EntityAlias = field.EntityAlias,
                Column = field.Column,
                DataType = field.Type,
                DynamicField = field.DynamicField
            });
        }

        foreach (var field in report.Fields)
        {
            if (field is null || field.Value is null)
            {
                throw new InvalidOperationException(
                    "A riport érvénytelen kimeneti mezőt tartalmaz.");
            }

            if (Same(field.Value.Kind, "REFERENCE"))
            {
                IncludeField(FindField(field.Value.Reference ?? ""));
            }
            else if (!Same(field.Value.Kind, "CONSTANT"))
            {
                throw new InvalidOperationException(
                    $"Ismeretlen riportérték-típus: {field.Value.Kind}");
            }
        }

        foreach (var reference in report.Grouping.GroupBy)
        {
            var field = FindField(reference);

            if (!field.CanGroup)
            {
                throw new InvalidOperationException(
                    $"A mező nem használható csoportosításra: {reference}");
            }

            IncludeField(field);
        }

        foreach (var reference in report.Grouping.SplitFilesBy)
        {
            var field = FindField(reference);

            if (!field.CanGroup)
            {
                throw new InvalidOperationException(
                    $"A mező nem használható fájlokra bontásra: {reference}");
            }

            IncludeField(field);
        }

        foreach (var reference in report.Grouping.SortBy)
        {
            var field = FindField(reference);

            if (!field.CanSort)
            {
                throw new InvalidOperationException(
                    $"A mező nem rendezhető: {reference}");
            }

            IncludeField(field);

            query.Sorts.Add(new ReportDataQuerySort
            {
                Reference = field.Reference,
                Direction = "ASC"
            });
        }

        var configuredRelations = new Dictionary<
            string,
            ReportRelationDefinition>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var relation in report.Relations)
        {
            if (relation is null ||
                string.IsNullOrWhiteSpace(relation.RelationCode) ||
                !configuredRelations.TryAdd(
                    relation.RelationCode,
                    relation))
            {
                throw new InvalidOperationException(
                    "Hiányzó vagy ismétlődő riportkapcsolat.");
            }

            IncludeRelation(relation.RelationCode);
        }

        var parameterReport = new ReportDefinition
        {
            Parameters = source.Parameters
                .Concat(report.Parameters)
                .ToList()
        };

        var parameterValues = parameterResolver.Resolve(
            parameterReport,
            request);

        var parameterCodes = parameterReport.Parameters
            .Select(parameter => parameter.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in source.Filters.Concat(report.Filters))
        {
            if (filter is null)
            {
                throw new InvalidOperationException(
                    "A riport null szűrőfeltételt tartalmaz.");
            }

            var field = FindField(filter.Field);
            EnsureFilterable(field);

            var operation = NormalizeOperator(filter.Operator);

            JsonElement? value = null;

            if (operation is not ("IS_NULL" or "IS_NOT_NULL"))
            {
                if (!string.IsNullOrWhiteSpace(filter.Parameter))
                {
                    if (!parameterCodes.Contains(filter.Parameter))
                    {
                        throw new InvalidOperationException(
                            $"A szűrő ismeretlen paraméterre hivatkozik: " +
                            filter.Parameter);
                    }

                    if (!parameterValues.TryGetValue(
                            filter.Parameter,
                            out var resolved))
                    {
                        continue;
                    }

                    value = resolved;
                }
                else
                {
                    value = filter.Constant;
                }

                if (value is null ||
                    value.Value.ValueKind is
                        JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    throw new ArgumentException(
                        $"Hiányzó szűrési érték: {filter.Field}");
                }

                value = PrepareFilterValue(field, operation, value.Value);
            }

            IncludeField(field);

            query.Filters.Add(CreateFilter(
                field,
                operation,
                value));
        }

        if (request.EmployeeIds is not null)
        {
            if (request.EmployeeIds.Count == 0 ||
                request.EmployeeIds.Any(id => id <= 0))
            {
                throw new ArgumentException(
                    "A dolgozólista nem lehet üres, " +
                    "és csak pozitív azonosítókat tartalmazhat.");
            }

            if (source.EmployeeIdField is null)
            {
                throw new InvalidOperationException(
                    "Ez az adatforrás nem támogat dolgozó szerinti szűrést.");
            }

            var field = FindField(source.EmployeeIdField);

            EnsureFilterable(field);
            IncludeField(field);

            query.Filters.Add(CreateFilter(
                field,
                "IN",
                JsonSerializer.SerializeToElement(
                    request.EmployeeIds.Distinct().ToArray())));
        }

        if (request.CompanyId.HasValue)
        {
            if (request.CompanyId.Value <= 0)
            {
                throw new ArgumentException(
                    "A cégazonosítónak pozitív számnak kell lennie.");
            }

            if (source.CompanyIdField is null)
            {
                throw new InvalidOperationException(
                    "Ehhez az adatforráshoz még nincs " +
                    "cég szerinti szűrési kapcsolat beállítva.");
            }

            var field = FindField(source.CompanyIdField);

            EnsureFilterable(field);
            IncludeField(field);

            query.Filters.Add(CreateFilter(
                field,
                "EQUALS",
                JsonSerializer.SerializeToElement(
                    request.CompanyId.Value)));
        }

        ApplyDateFilter(
            report.DateFilter,
            source.Validity,
            source.Root.Alias,
            request,
            query.Filters,
            FindField,
            IncludeField);

        foreach (var join in query.Joins.ToArray())
        {
            if (!configuredRelations.TryGetValue(
                    join.Code,
                    out var configuration))
            {
                continue;
            }

            if (configuration.DateFilter is null)
            {
                throw new InvalidOperationException(
                    $"Hiányzó kapcsolati dátumbeállítás: {join.Code}");
            }

            if (!IsOneOf(configuration.JoinType, "LEFT", "INNER"))
            {
                throw new InvalidOperationException(
                    $"Érvénytelen kapcsolástípus: {configuration.JoinType}");
            }

            join.JoinType = configuration.JoinType.ToUpperInvariant();

            ApplyDateFilter(
                configuration.DateFilter,
                relations[join.Code].Validity,
                join.Target.Alias,
                request,
                join.Filters,
                FindField,
                IncludeField);
        }

        if (query.Fields.Count == 0)
        {
            var anchor = source.Fields.FirstOrDefault(field =>
                field.RequiredRelationCode is null &&
                field.DynamicField is null);

            if (anchor is null)
            {
                throw new InvalidOperationException(
                    "Az adatforrás nem tartalmaz lekérdezhető alapmezőt.");
            }

            IncludeField(anchor);
        }

        ConfigureLatestRecords(query, source);

        if (query.Fields.Any(item => item.DynamicField is not null))
        {
            query.DynamicFieldAsOfDate = ResolveDynamicFieldDate(
                report,
                request);
        }

        return query;
    }

    private static void ApplyDateFilter(
        ReportDateFilterDefinition configuration,
        ReportDataValidityDefinition? sourceValidity,
        string expectedAlias,
        ReportExportRequest request,
        List<ReportDataQueryFilter> destination,
        Func<string, ReportDataFieldDefinition> findField,
        Action<ReportDataFieldDefinition> includeField)
    {
        var mode = configuration.Mode?.Trim().ToUpperInvariant();

        if (mode == "NONE")
        {
            return;
        }

        var dateReference =
            configuration.DateField ?? sourceValidity?.DateField;

        var fromReference =
            configuration.ValidFromField ?? sourceValidity?.ValidFromField;

        var toReference =
            configuration.ValidToField ?? sourceValidity?.ValidToField;

        ReportDataFieldDefinition ResolveDateField(string reference)
        {
            var field = findField(reference);

            EnsureFilterable(field);

            if (!Same(field.EntityAlias, expectedAlias))
            {
                throw new InvalidOperationException(
                    $"A dátumszűrő másik táblára hivatkozik: {reference}");
            }

            if (!IsOneOf(field.Type, "date", "datetime", "month"))
            {
                throw new InvalidOperationException(
                    $"A mező nem használható dátumszűrésre: {reference}");
            }

            includeField(field);

            return field;
        }

        if (mode == "MONTH_RANGE")
        {
            var from = ParseMonth(request.PeriodFrom);
            var to = ParseMonth(request.PeriodTo);

            if (from > to)
            {
                throw new ArgumentException(
                    "A kezdő hónap nem lehet későbbi a záró hónapnál.");
            }

            if (dateReference is null)
            {
                throw new InvalidOperationException(
                    "A havi szűréshez nincs dátum- vagy periódusmező megadva.");
            }

            var field = ResolveDateField(dateReference);

            if (!Same(field.Type, "month"))
            {
                throw new InvalidOperationException(
                    "A havi periódusszűréshez month típusú mező szükséges.");
            }

            destination.Add(CreateFilter(
                field,
                "GREATER_THAN_OR_EQUAL",
                JsonSerializer.SerializeToElement(
                    from.ToString("yyyyMM", CultureInfo.InvariantCulture))));

            destination.Add(CreateFilter(
                field,
                "LESS_THAN_OR_EQUAL",
                JsonSerializer.SerializeToElement(
                    to.ToString("yyyyMM", CultureInfo.InvariantCulture))));

            return;
        }

        DateOnly start;
        DateOnly end;

        if (mode == "AS_OF_DATE")
        {
            start = request.AsOfDate
                ?? throw new ArgumentException(
                    "Az érvényességi dátum megadása kötelező.");

            end = start;
        }
        else if (mode == "DATE_RANGE")
        {
            start = request.DateFrom
                ?? throw new ArgumentException(
                    "A kezdő dátum megadása kötelező.");

            end = request.DateTo
                ?? throw new ArgumentException(
                    "A záró dátum megadása kötelező.");

            if (start > end)
            {
                throw new ArgumentException(
                    "A kezdő dátum nem lehet későbbi a záró dátumnál.");
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Ismeretlen dátumszűrési mód: {configuration.Mode}");
        }

        if (fromReference is not null)
        {
            var fromField = ResolveDateField(fromReference);

            EnsureCalendarDateType(fromField);

            destination.Add(CreateUpperDateFilter(fromField, end));

            if (toReference is not null)
            {
                var toField = ResolveDateField(toReference);

                EnsureCalendarDateType(toField);

                var filter = CreateFilter(
                    toField,
                    "GREATER_THAN_OR_EQUAL",
                    SerializeDate(start));

                filter.IncludeNull = true;
                destination.Add(filter);
            }

            return;
        }

        if (dateReference is null)
        {
            throw new InvalidOperationException(
                "Az adatforráshoz nincs dátumszűrésre alkalmas mező beállítva.");
        }

        var dateField = ResolveDateField(dateReference);

        EnsureCalendarDateType(dateField);

        destination.Add(CreateFilter(
            dateField,
            "GREATER_THAN_OR_EQUAL",
            SerializeDate(start)));

        destination.Add(CreateUpperDateFilter(dateField, end));
    }

    private static ReportDataQueryFilter CreateUpperDateFilter(
        ReportDataFieldDefinition field,
        DateOnly end)
    {
        if (Same(field.Type, "datetime"))
        {
            if (end == DateOnly.MaxValue)
            {
                throw new ArgumentException(
                    "Időbélyeg szűrésénél a záró dátum nem lehet 9999-12-31.");
            }

            return CreateFilter(
                field,
                "LESS_THAN",
                SerializeDate(end.AddDays(1)));
        }

        return CreateFilter(
            field,
            "LESS_THAN_OR_EQUAL",
            SerializeDate(end));
    }
    private static DateOnly ResolveDynamicFieldDate(
    ReportDefinition report,
    ReportExportRequest request)
    {
        var mode = report.DateFilter.Mode?
            .Trim()
            .ToUpperInvariant();

        if (mode == "MONTH_RANGE")
        {
            return LastDayOfMonth(request.PeriodTo);
        }

        if (mode == "DATE_RANGE")
        {
            return request.DateTo
                ?? throw new ArgumentException(
                    "A dinamikus mezők kiolvasásához add meg a záró dátumot.");
        }

        if (mode == "AS_OF_DATE")
        {
            return request.AsOfDate
                ?? throw new ArgumentException(
                    "A dinamikus mezők kiolvasásához add meg az érvényességi dátumot.");
        }

        if (request.AsOfDate.HasValue)
            return request.AsOfDate.Value;

        if (request.DateTo.HasValue)
            return request.DateTo.Value;

        if (!string.IsNullOrWhiteSpace(request.PeriodTo))
            return LastDayOfMonth(request.PeriodTo);

        throw new ArgumentException(
            "A dinamikus mezők kiolvasásához érvényességi dátum vagy riportidőszak szükséges.");
    }

    private static DateOnly LastDayOfMonth(string period)
    {
        var month = ParseMonth(period);

        return new DateOnly(
            month.Year,
            month.Month,
            DateTime.DaysInMonth(
                month.Year,
                month.Month));
    }

    private static void ConfigureLatestRecords(
        ReportDataQueryDefinition query,
        ReportDataSourceDefinition source)
    {
        if (!Same(source.Root.Schema, "dbo") ||
            !Same(source.Root.Entity, "PayrollResult"))
        {
            return;
        }

        query.LatestRecords = new ReportDataQueryLatestRecords
        {
            PartitionColumns =
            [
                "EmployeeId",
                "Period",
                "PayrollElementId",
                "ResultType"
            ],
            OrderBy =
            [
                new ReportDataQueryRecordOrder
                {
                    Column = "RunDate",
                    Direction = "DESC"
                },
                new ReportDataQueryRecordOrder
                {
                    Column = "PayrollResultId",
                    Direction = "DESC"
                }
            ]
        };
    }

    private static ReportDataQueryFilter CreateFilter(
        ReportDataFieldDefinition field,
        string operation,
        JsonElement? value)
    {
        return new ReportDataQueryFilter
        {
            EntityAlias = field.EntityAlias,
            Column = field.Column,
            DataType = field.Type,
            Operator = operation,
            Value = value,
            DynamicField = field.DynamicField
        };
    }

    private static JsonElement PrepareFilterValue(
        ReportDataFieldDefinition field,
        string operation,
        JsonElement value)
    {
        if (operation is "IN" or "NOT_IN")
        {
            if (value.ValueKind != JsonValueKind.Array ||
                value.GetArrayLength() == 0)
            {
                throw new ArgumentException(
                    $"A listás szűréshez nem üres lista szükséges: " +
                    field.Reference);
            }

            var items = value.EnumerateArray()
                .Select(item => PrepareScalarValue(field, item))
                .ToArray();

            return JsonSerializer.SerializeToElement(items);
        }

        if (operation is "CONTAINS" or "STARTS_WITH")
        {
            if (!IsOneOf(field.Type, "text", "option") ||
                value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException(
                    $"A szöveges kereséshez szövegmező és szöveges érték szükséges: " +
                    field.Reference);
            }
        }

        return PrepareScalarValue(field, value);
    }

    private static JsonElement PrepareScalarValue(
        ReportDataFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind is
            JsonValueKind.Null or
            JsonValueKind.Undefined or
            JsonValueKind.Array or
            JsonValueKind.Object)
        {
            throw new ArgumentException(
                $"A szűrés egyszerű, nem null értéket vár: {field.Reference}");
        }

        if (Same(field.Type, "month"))
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException(
                    "A periódusszűrés szöveges havi értéket vár.");
            }

            var text = value.GetString()!;

            if (text.Length == 6)
            {
                text = $"{text[..4]}-{text[4..]}";
            }

            var month = ParseMonth(text);

            return JsonSerializer.SerializeToElement(
                month.ToString("yyyyMM", CultureInfo.InvariantCulture));
        }

        return value.Clone();
    }

    private static void EnsureFilterable(
        ReportDataFieldDefinition field)
    {
        if (!field.CanFilter)
        {
            throw new InvalidOperationException(
                $"A mező nem használható szűrésre: {field.Reference}");
        }
    }

    private static void EnsureCalendarDateType(
        ReportDataFieldDefinition field)
    {
        if (!IsOneOf(field.Type, "date", "datetime"))
        {
            throw new InvalidOperationException(
                $"A napi dátumszűrés date vagy datetime mezőt vár: " +
                field.Reference);
        }
    }

    private static string NormalizeOperator(string operation)
    {
        var normalized = operation?.Trim().ToUpperInvariant();

        return normalized switch
        {
            "EQUALS" or
            "NOT_EQUALS" or
            "GREATER_THAN" or
            "GREATER_THAN_OR_EQUAL" or
            "LESS_THAN" or
            "LESS_THAN_OR_EQUAL" or
            "CONTAINS" or
            "STARTS_WITH" or
            "IS_NULL" or
            "IS_NOT_NULL" or
            "IN" or
            "NOT_IN" => normalized,

            _ => throw new InvalidOperationException(
                $"Ismeretlen szűrési művelet: {operation}")
        };
    }

    private static DateOnly ParseMonth(string value)
    {
        if (value is null ||
            value.Length != 7 ||
            !DateOnly.TryParseExact(
                $"{value}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month))
        {
            throw new ArgumentException(
                "A havi időszakot yyyy-MM formátumban kell megadni.");
        }

        return month;
    }

    private static JsonElement SerializeDate(DateOnly date)
    {
        return JsonSerializer.SerializeToElement(
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static bool IsOneOf(
        string? value,
        params string[] values)
    {
        return values.Any(item => Same(value, item));
    }

    private static bool Same(string? first, string? second)
    {
        return string.Equals(
            first,
            second,
            StringComparison.OrdinalIgnoreCase);
    }
}