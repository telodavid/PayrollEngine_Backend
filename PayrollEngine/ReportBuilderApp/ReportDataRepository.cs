using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportDataRepository(
    PayrollDbContext database,
    ReportDataCatalogService catalogService,
    ReportDataQueryBuilder queryBuilder)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        LoadPayrollResultsAsync(
            ReportDefinition report,
            ReportExportRequest request,
            CancellationToken cancellationToken = default)
    {
        return LoadAsync(report, request, cancellationToken);
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        LoadAsync(
            ReportDefinition report,
            ReportExportRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        if (request.CompanyId is <= 0)
        {
            throw new ArgumentException(
                "A cégazonosítónak pozitív számnak kell lennie.");
        }

        var source = await catalogService.GetSourceAsync(
            report.DataSource,
            cancellationToken);

        var useCompanyEmployeeFilter =
            request.CompanyId.HasValue &&
            source.CompanyIdField is null &&
            source.EmployeeIdField is not null;

        var effectiveRequest = useCompanyEmployeeFilter
            ? CopyWithoutCompanyFilter(request)
            : request;

        var query = queryBuilder.Build(
            report,
            effectiveRequest,
            source);

        ReportDataFieldDefinition? companyEmployeeField = null;

        if (useCompanyEmployeeFilter)
        {
            companyEmployeeField = source.Fields.FirstOrDefault(field =>
                string.Equals(
                    field.Reference,
                    source.EmployeeIdField,
                    StringComparison.OrdinalIgnoreCase));

            if (companyEmployeeField is null ||
                companyEmployeeField.RequiredRelationCode is not null ||
                companyEmployeeField.DynamicField is not null)
            {
                throw new InvalidOperationException(
                    "A cégszűréshez a kiinduló tábla " +
                    "dolgozóazonosító mezője szükséges.");
            }
        }

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (useCompanyEmployeeFilter)
            {
                var companyEmployeeIds =
                    await LoadCompanyEmployeeIdsAsync(
                        connection,
                        report,
                        request,
                        cancellationToken);

                if (request.EmployeeIds is not null)
                {
                    var selectedIds = request.EmployeeIds.ToHashSet();

                    companyEmployeeIds = companyEmployeeIds
                        .Where(selectedIds.Contains)
                        .ToArray();
                }

                if (companyEmployeeIds.Count == 0)
                {
                    return [];
                }

                query.Filters.Add(new ReportDataQueryFilter
                {
                    EntityAlias = companyEmployeeField!.EntityAlias,
                    Column = companyEmployeeField.Column,
                    DataType = companyEmployeeField.Type,
                    Operator = "IN",
                    Value = JsonSerializer.SerializeToElement(
                        companyEmployeeIds)
                });
            }

            var queryJson = JsonSerializer.Serialize(
                query,
                JsonOptions);

            await using var command = connection.CreateCommand();

            command.CommandText =
                "dbo.usp_ReportBuilder_ExecuteQuery";

            command.CommandType = CommandType.StoredProcedure;

            AddParameter(
                command,
                "@QueryJson",
                queryJson,
                DbType.String,
                -1);

            var rows =
                new List<IReadOnlyDictionary<string, object?>>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var columnNames = new string[reader.FieldCount];

            var knownNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = reader.GetName(index);

                if (string.IsNullOrWhiteSpace(name) ||
                    !knownNames.Add(name))
                {
                    throw new InvalidOperationException(
                        "A riportlekérdezés hiányzó vagy ismétlődő " +
                        $"oszlopnevet adott vissza: {name}");
                }

                columnNames[index] = name;
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < columnNames.Length; index++)
                {
                    row.Add(
                        columnNames[index],
                        reader.IsDBNull(index)
                            ? null
                            : reader.GetValue(index));
                }

                rows.Add(row);
            }

            return rows;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyList<int>>
        LoadCompanyEmployeeIdsAsync(
            DbConnection connection,
            ReportDefinition report,
            ReportExportRequest request,
            CancellationToken cancellationToken)
    {
        var (dateFrom, dateTo) = GetCompanyFilterDates(
            report,
            request);

        await using var command = connection.CreateCommand();

        command.CommandText =
            "dbo.usp_ReportBuilder_GetCompanyEmployeeIds";

        command.CommandType = CommandType.StoredProcedure;

        AddParameter(
            command,
            "@CompanyId",
            request.CompanyId!.Value,
            DbType.Int32);

        AddParameter(
            command,
            "@DateFrom",
            dateFrom.HasValue
                ? (object)dateFrom.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value,
            DbType.Date);

        AddParameter(
            command,
            "@DateTo",
            dateTo.HasValue
                ? (object)dateTo.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value,
            DbType.Date);

        var employeeIds = new HashSet<int>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        var employeeIdOrdinal = reader.GetOrdinal("EmployeeId");

        while (await reader.ReadAsync(cancellationToken))
        {
            employeeIds.Add(reader.GetInt32(employeeIdOrdinal));
        }

        return employeeIds.OrderBy(id => id).ToArray();
    }

    private static (DateOnly? From, DateOnly? To)
        GetCompanyFilterDates(
            ReportDefinition report,
            ReportExportRequest request)
    {
        var mode = report.DateFilter.Mode?.Trim().ToUpperInvariant();

        switch (mode)
        {
            case "NONE":
                return (null, null);

            case "AS_OF_DATE":
                {
                    var date = request.AsOfDate
                        ?? throw new ArgumentException(
                            "Az érvényességi dátum megadása kötelező.");

                    return (date, date);
                }

            case "DATE_RANGE":
                {
                    var from = request.DateFrom
                        ?? throw new ArgumentException(
                            "A kezdő dátum megadása kötelező.");

                    var to = request.DateTo
                        ?? throw new ArgumentException(
                            "A záró dátum megadása kötelező.");

                    if (from > to)
                    {
                        throw new ArgumentException(
                            "A kezdő dátum nem lehet későbbi a záró dátumnál.");
                    }

                    return (from, to);
                }

            case "MONTH_RANGE":
                {
                    var from = ParseMonth(request.PeriodFrom);
                    var toMonth = ParseMonth(request.PeriodTo);

                    if (from > toMonth)
                    {
                        throw new ArgumentException(
                            "A kezdő hónap nem lehet későbbi a záró hónapnál.");
                    }

                    var to = new DateOnly(
                        toMonth.Year,
                        toMonth.Month,
                        DateTime.DaysInMonth(
                            toMonth.Year,
                            toMonth.Month));

                    return (from, to);
                }

            default:
                throw new InvalidOperationException(
                    $"Ismeretlen dátumszűrési mód: {report.DateFilter.Mode}");
        }
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

    private static ReportExportRequest CopyWithoutCompanyFilter(
        ReportExportRequest request)
    {
        return new ReportExportRequest
        {
            Format = request.Format,
            PeriodFrom = request.PeriodFrom,
            PeriodTo = request.PeriodTo,
            AsOfDate = request.AsOfDate,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            EmployeeIds = request.EmployeeIds,
            CompanyId = null,
            ParameterValues = request.ParameterValues
        };
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value,
        DbType type,
        int? size = null)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;

        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        command.Parameters.Add(parameter);
    }
}