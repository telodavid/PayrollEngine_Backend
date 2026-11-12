using System.Globalization;
using System.Text.Json;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportEmployeeDocumentBuilder(
    ReportDataCatalogService catalogService,
    ReportDataRepository dataRepository,
    ReportOutputRowBuilder rowBuilder)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ReportEmployeeDocumentData>> BuildAsync(
        ReportDefinition report,
        ReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        if (!Same(report.LayoutMode, "EMPLOYEE_DOCUMENT"))
        {
            throw new InvalidOperationException(
                "Ez az összeállító dolgozónkénti dokumentumot készít.");
        }

        if (!Same(request.Format, "XLSX") &&
            !Same(request.Format, "PDF"))
        {
            throw new ArgumentException(
                "A dolgozónkénti dokumentum XLSX vagy PDF formátumban exportálható.");
        }

        var configuration = report.EmployeeDocument
            ?? throw new InvalidOperationException(
                "Hiányzik a dolgozónkénti dokumentum konfigurációja.");

        if (configuration.Header is null ||
            configuration.AdditionalSections is null)
        {
            throw new InvalidOperationException(
                "A dokumentum fejléc- vagy adatrész-konfigurációja hiányos.");
        }

        var catalog = await catalogService.GetCatalogAsync(
            cancellationToken);

        var sources = catalog.Sources.ToDictionary(
            item => item.Code,
            StringComparer.OrdinalIgnoreCase);

        var mainDefinition = Clone(report);

        mainDefinition.LayoutMode = "TABLE";
        mainDefinition.EmployeeDocument = null;

        var mainSource = FindEmployeeSource(
            sources,
            mainDefinition.DataSource);

        ValidateBlock(mainDefinition);

        if (mainDefinition.Grouping.SplitFilesBy.Any(reference =>
                !Same(reference, mainSource.EmployeeIdField)))
        {
            throw new InvalidOperationException(
                "Dolgozónkénti dokumentumban a fájlokra bontást " +
                "a rendszer dolgozó szerint végzi. " +
                "Más fájlbontási mezőt távolíts el.");
        }

        ReportDefinition? headerDefinition = null;
        ReportDataSourceDefinition? headerSource = null;

        if (configuration.Header.IsEnabled &&
            configuration.Header.Fields.Count > 0)
        {
            headerDefinition = CreateHeaderDefinition(
                report,
                configuration.Header);

            headerSource = FindEmployeeSource(
                sources,
                headerDefinition.DataSource);

            ValidateBlock(headerDefinition);
        }

        var sections = new List<SectionBlock>();

        var sectionCodes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "__main"
        };

        foreach (var section in configuration.AdditionalSections)
        {
            if (section is null)
            {
                throw new InvalidOperationException(
                    "A dokumentum null adatrészt tartalmaz.");
            }

            if (!section.IsEnabled)
                continue;

            if (string.IsNullOrWhiteSpace(section.Code) ||
                !sectionCodes.Add(section.Code))
            {
                throw new InvalidOperationException(
                    "Az adatrészek kódja kötelező és nem ismétlődhet.");
            }

            if (string.IsNullOrWhiteSpace(section.Title))
            {
                throw new InvalidOperationException(
                    $"Hiányzik az adatrész megnevezése: {section.Code}");
            }

            var definition = CreateSectionDefinition(
                report,
                section);

            var source = FindEmployeeSource(
                sources,
                definition.DataSource);

            ValidateBlock(definition);

            if (definition.Grouping.SplitFilesBy.Count > 0)
            {
                throw new InvalidOperationException(
                    "A további adatrész nem tartalmazhat " +
                    $"külön fájlbontást: {section.Title}");
            }

            sections.Add(new SectionBlock(
                section,
                definition,
                source));
        }

        var knownParameters = mainDefinition.Parameters
            .Select(item => item.Code)
            .Concat(mainSource.Parameters.Select(item => item.Code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (headerDefinition is not null && headerSource is not null)
        {
            knownParameters.UnionWith(
                headerDefinition.Parameters.Select(item => item.Code));

            knownParameters.UnionWith(
                headerSource.Parameters.Select(item => item.Code));
        }

        foreach (var section in sections)
        {
            knownParameters.UnionWith(
                section.Definition.Parameters.Select(item => item.Code));

            knownParameters.UnionWith(
                section.Source.Parameters.Select(item => item.Code));
        }

        if (request.ParameterValues is not null)
        {
            foreach (var code in request.ParameterValues.Keys)
            {
                if (!knownParameters.Contains(code))
                {
                    throw new ArgumentException(
                        $"Ismeretlen dokumentumparaméter: {code}");
                }
            }
        }

        var sharedRequest = CreateSharedRequest(
            mainDefinition,
            request);

        var mainQueryDefinition = Clone(mainDefinition);

        EnsureEmployeeField(
            mainQueryDefinition,
            mainSource.EmployeeIdField!);

        var mainRequest = CreateBlockRequest(
            sharedRequest,
            mainDefinition,
            mainSource,
            null);

        var mainRows = await dataRepository.LoadAsync(
            mainQueryDefinition,
            mainRequest,
            cancellationToken);

        var employeeGroups = mainRows
            .GroupBy(row => ReadEmployeeId(
                row,
                mainSource.EmployeeIdField!))
            .OrderBy(group => group.Key)
            .ToArray();

        var documents = new List<ReportEmployeeDocumentData>();

        foreach (var employeeGroup in employeeGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var employeeId = employeeGroup.Key;
            var employeeRows = employeeGroup.ToArray();

            var document = new ReportEmployeeDocumentData
            {
                ReportCode = report.ReportCode,
                Title = report.Name,
                EmployeeId = employeeId
            };

            foreach (var value in employeeRows[0])
                document.FileNameValues[value.Key] = value.Value;

            if (headerDefinition is not null &&
                headerSource is not null)
            {
                var headerRequest = CreateBlockRequest(
                    sharedRequest,
                    headerDefinition,
                    headerSource,
                    employeeId);

                var headerRows = await dataRepository.LoadAsync(
                    headerDefinition,
                    headerRequest,
                    cancellationToken);

                var headerOutput = rowBuilder.Build(
                    headerDefinition,
                    headerRows);

                AddHeader(
                    document,
                    headerDefinition,
                    headerOutput);
            }

            document.Sections.Add(new ReportDocumentSectionData
            {
                Code = "__main",
                Title = string.IsNullOrWhiteSpace(
                    configuration.MainSectionTitle)
                    ? report.Name
                    : configuration.MainSectionTitle,
                WorksheetName = string.IsNullOrWhiteSpace(
                    configuration.MainWorksheetName)
                    ? "Adatok"
                    : configuration.MainWorksheetName,
                StartNewPage = false,
                Fields = mainDefinition.Fields,
                OutputRows = rowBuilder.Build(
                    mainDefinition,
                    employeeRows)
            });

            foreach (var section in sections)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sectionRequest = CreateBlockRequest(
                    sharedRequest,
                    section.Definition,
                    section.Source,
                    employeeId);

                var sourceRows = await dataRepository.LoadAsync(
                    section.Definition,
                    sectionRequest,
                    cancellationToken);

                document.Sections.Add(new ReportDocumentSectionData
                {
                    Code = section.Configuration.Code,
                    Title = section.Configuration.Title,
                    WorksheetName = string.IsNullOrWhiteSpace(
                        section.Configuration.WorksheetName)
                        ? section.Configuration.Title
                        : section.Configuration.WorksheetName,
                    StartNewPage = section.Configuration.StartNewPage,
                    Fields = section.Definition.Fields,
                    OutputRows = rowBuilder.Build(
                        section.Definition,
                        sourceRows)
                });
            }

            document.FileNameValues["employeeId"] = employeeId;
            document.FileNameValues["reportCode"] = report.ReportCode;
            document.FileNameValues["period"] = sharedRequest.PeriodFrom;
            document.FileNameValues["periodFrom"] = sharedRequest.PeriodFrom;
            document.FileNameValues["periodTo"] = sharedRequest.PeriodTo;
            document.FileNameValues["dateFrom"] = sharedRequest.DateFrom;
            document.FileNameValues["dateTo"] = sharedRequest.DateTo;
            document.FileNameValues["asOfDate"] = sharedRequest.AsOfDate;

            documents.Add(document);
        }

        return documents;
    }

    private static ReportDataSourceDefinition FindEmployeeSource(
        IReadOnlyDictionary<string, ReportDataSourceDefinition> sources,
        string code)
    {
        if (!sources.TryGetValue(code, out var source))
        {
            throw new InvalidOperationException(
                $"Ismeretlen adatforrás: {code}");
        }

        if (string.IsNullOrWhiteSpace(source.EmployeeIdField))
        {
            throw new InvalidOperationException(
                "Az adatforrás nem kapcsolható egyértelműen " +
                $"dolgozóhoz: {source.DisplayName}");
        }

        return source;
    }

    private static void ValidateBlock(ReportDefinition definition)
    {
        if (definition.Fields is null ||
            definition.Fields.Count == 0 ||
            definition.Grouping is null ||
            definition.Relations is null ||
            definition.Parameters is null ||
            definition.Filters is null ||
            definition.DateFilter is null)
        {
            throw new InvalidOperationException(
                $"Hiányos adatrész-konfiguráció: {definition.Name}");
        }

        var outputNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in definition.Fields)
        {
            if (item is null ||
                item.Value is null ||
                string.IsNullOrWhiteSpace(item.OutputName) ||
                !outputNames.Add(item.OutputName))
            {
                throw new InvalidOperationException(
                    $"Hiányzó vagy ismétlődő kimeneti mező: {definition.Name}");
            }
        }
    }

    private static ReportDefinition CreateHeaderDefinition(
        ReportDefinition report,
        ReportDocumentHeaderDefinition header)
    {
        return Clone(new ReportDefinition
        {
            ReportCode = report.ReportCode,
            Name = $"{report.Name} – fejléc",
            DataSource = header.DataSource,
            DateFilter = header.DateFilter,
            Relations = header.Relations,
            Fields = header.Fields,
            Parameters = header.Parameters,
            Filters = header.Filters,
            Grouping = new ReportGroupingDefinition
            {
                RowMode = "DETAIL"
            }
        });
    }

    private static ReportDefinition CreateSectionDefinition(
        ReportDefinition report,
        ReportDocumentSectionDefinition section)
    {
        return Clone(new ReportDefinition
        {
            ReportCode = report.ReportCode,
            Name = section.Title,
            DataSource = section.DataSource,
            DateFilter = section.DateFilter,
            Relations = section.Relations,
            Fields = section.Fields,
            Parameters = section.Parameters,
            Filters = section.Filters,
            Grouping = section.Grouping
        });
    }

    private static void EnsureEmployeeField(
        ReportDefinition definition,
        string reference)
    {
        if (definition.Fields.Any(item =>
                Same(item.Value.Kind, "REFERENCE") &&
                Same(item.Value.Reference, reference)))
        {
            return;
        }

        var code = $"__scope_{Guid.NewGuid():N}";

        definition.Fields.Add(new ReportFieldDefinition
        {
            Code = code,
            DisplayName = "Dolgozóazonosító",
            OutputName = code,
            Value = new ReportValueDefinition
            {
                Kind = "REFERENCE",
                Reference = reference
            }
        });
    }

    private static int ReadEmployeeId(
        IReadOnlyDictionary<string, object?> row,
        string reference)
    {
        if (!row.TryGetValue(reference, out var value) ||
            value is null)
        {
            throw new InvalidOperationException(
                $"A lekérdezett sorból hiányzik a dolgozóazonosító: {reference}");
        }

        try
        {
            var employeeId = Convert.ToInt32(
                value,
                CultureInfo.InvariantCulture);

            if (employeeId <= 0)
            {
                throw new InvalidOperationException(
                    "A dolgozóazonosítónak pozitívnak kell lennie.");
            }

            return employeeId;
        }
        catch (Exception exception)
            when (exception is FormatException or
                  InvalidCastException or
                  OverflowException)
        {
            throw new InvalidOperationException(
                $"Érvénytelen dolgozóazonosító: {reference}",
                exception);
        }
    }

    private static void AddHeader(
        ReportEmployeeDocumentData document,
        ReportDefinition definition,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nem található fejlécadat a dolgozóhoz: {document.EmployeeId}");
        }

        foreach (var item in definition.Fields)
        {
            rows[0].TryGetValue(item.OutputName, out var value);

            var signature = JsonSerializer.Serialize(
                value,
                JsonOptions);

            foreach (var row in rows.Skip(1))
            {
                row.TryGetValue(item.OutputName, out var otherValue);

                var otherSignature = JsonSerializer.Serialize(
                    otherValue,
                    JsonOptions);

                if (!string.Equals(
                        signature,
                        otherSignature,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A fejléc mezőjéhez több eltérő érték tartozik. " +
                        $"Dolgozó: {document.EmployeeId}; " +
                        $"mező: {item.DisplayName}. " +
                        "Pontosítsd a fejléc szűrését vagy érvényességét.");
                }
            }

            document.Header.Add(new ReportDocumentHeaderValue
            {
                Code = item.Code,
                Label = item.DisplayName,
                OutputName = item.OutputName,
                Value = value,
                Format = item.Format
            });

            document.FileNameValues.TryAdd(
                item.OutputName,
                value);
        }
    }

    private static ReportExportRequest CreateBlockRequest(
        ReportExportRequest shared,
        ReportDefinition definition,
        ReportDataSourceDefinition source,
        int? employeeId)
    {
        var parameterCodes = definition.Parameters
            .Select(item => item.Code)
            .Concat(source.Parameters.Select(item => item.Code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, JsonElement>? values = null;

        if (shared.ParameterValues is not null)
        {
            values = new Dictionary<string, JsonElement>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in shared.ParameterValues)
            {
                if (!parameterCodes.Contains(item.Key))
                    continue;

                if (!values.TryAdd(
                        item.Key,
                        item.Value.Clone()))
                {
                    throw new ArgumentException(
                        $"Ismétlődő dokumentumparaméter: {item.Key}");
                }
            }
        }

        return new ReportExportRequest
        {
            Format = shared.Format,
            PeriodFrom = shared.PeriodFrom,
            PeriodTo = shared.PeriodTo,
            AsOfDate = shared.AsOfDate,
            DateFrom = shared.DateFrom,
            DateTo = shared.DateTo,
            EmployeeIds = employeeId.HasValue
                ? new List<int> { employeeId.Value }
                : shared.EmployeeIds?.ToList(),
            CompanyId = shared.CompanyId,
            ParameterValues = values
        };
    }

    private static ReportExportRequest CreateSharedRequest(
        ReportDefinition mainDefinition,
        ReportExportRequest request)
    {
        var result = new ReportExportRequest
        {
            Format = request.Format,
            EmployeeIds = request.EmployeeIds?.ToList(),
            CompanyId = request.CompanyId,
            ParameterValues = request.ParameterValues
        };

        var mode = mainDefinition.DateFilter.Mode?
            .Trim()
            .ToUpperInvariant() ?? "NONE";

        DateOnly? from;
        DateOnly? to;

        switch (mode)
        {
            case "MONTH_RANGE":
                {
                    var range = ReadMonthRange(request);
                    from = range.From;
                    to = range.To;
                    break;
                }

            case "DATE_RANGE":
                {
                    var range = ReadDateRange(request);
                    from = range.From;
                    to = range.To;
                    break;
                }

            case "AS_OF_DATE":
                {
                    if (!request.AsOfDate.HasValue)
                    {
                        throw new ArgumentException(
                            "Add meg a fő riport érvényességi dátumát.");
                    }

                    from = request.AsOfDate.Value;
                    to = request.AsOfDate.Value;
                    break;
                }

            case "NONE":
                {
                    if (request.DateFrom.HasValue ||
                        request.DateTo.HasValue)
                    {
                        var range = ReadDateRange(request);
                        from = range.From;
                        to = range.To;
                    }
                    else if (
                        !string.IsNullOrWhiteSpace(request.PeriodFrom) ||
                        !string.IsNullOrWhiteSpace(request.PeriodTo))
                    {
                        var range = ReadMonthRange(request);
                        from = range.From;
                        to = range.To;
                    }
                    else
                    {
                        from = request.AsOfDate;
                        to = request.AsOfDate;
                    }

                    break;
                }

            default:
                throw new ArgumentException(
                    $"Ismeretlen dátumszűrési mód a fő riportban: {mode}");
        }

        if (!from.HasValue || !to.HasValue)
            return result;

        result.DateFrom = from.Value;
        result.DateTo = to.Value;
        result.AsOfDate = to.Value;

        result.PeriodFrom = from.Value.ToString(
            "yyyy-MM",
            CultureInfo.InvariantCulture);

        result.PeriodTo = to.Value.ToString(
            "yyyy-MM",
            CultureInfo.InvariantCulture);

        return result;
    }

    private static (DateOnly From, DateOnly To) ReadMonthRange(
        ReportExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PeriodFrom) ||
            string.IsNullOrWhiteSpace(request.PeriodTo))
        {
            throw new ArgumentException(
                "Add meg a fő riport kezdő és záró hónapját.");
        }

        var from = ParseMonth(request.PeriodFrom);
        var lastMonth = ParseMonth(request.PeriodTo);

        if (from > lastMonth)
        {
            throw new ArgumentException(
                "A kezdő hónap nem lehet későbbi a záró hónapnál.");
        }

        var to = new DateOnly(
            lastMonth.Year,
            lastMonth.Month,
            DateTime.DaysInMonth(
                lastMonth.Year,
                lastMonth.Month));

        return (from, to);
    }

    private static (DateOnly From, DateOnly To) ReadDateRange(
        ReportExportRequest request)
    {
        if (!request.DateFrom.HasValue ||
            !request.DateTo.HasValue)
        {
            throw new ArgumentException(
                "Add meg a fő riport kezdő és záró dátumát.");
        }

        var from = request.DateFrom.Value;
        var to = request.DateTo.Value;

        if (from > to)
        {
            throw new ArgumentException(
                "A kezdő dátum nem lehet későbbi a záró dátumnál.");
        }

        return (from, to);
    }

    private static DateOnly ParseMonth(string value)
    {
        var text = value.Trim();

        if (text.Length != 7 ||
            !DateOnly.TryParseExact(
                $"{text}-01",
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

    private static ReportDefinition Clone(
        ReportDefinition definition)
    {
        return JsonSerializer.Deserialize<ReportDefinition>(
            JsonSerializer.Serialize(definition, JsonOptions),
            JsonOptions)
            ?? throw new InvalidOperationException(
                "A riportdefiníció másolása sikertelen.");
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

    private sealed record SectionBlock(
        ReportDocumentSectionDefinition Configuration,
        ReportDefinition Definition,
        ReportDataSourceDefinition Source);
}