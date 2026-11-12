using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollRunDataRepository(PayrollDbContext database)
{
    public async Task<IReadOnlyDictionary<string, object?>>
        LoadEmployeePeriodDataAsync(
            int employeeId,
            DateOnly periodStart,
            CancellationToken cancellationToken = default)
    {
        if (employeeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(employeeId));
        }

        if (periodStart.Day != 1)
        {
            throw new ArgumentException(
                "A periódus kezdete csak a hónap első napja lehet.",
                nameof(periodStart));
        }

        var data = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);

        var collections =
            new Dictionary<
                string,
                Dictionary<string, Dictionary<string, object?>>>(
                StringComparer.OrdinalIgnoreCase);

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Alapadatok: dolgozó, munkaviszonyok, dolgozói dinamikus mezők.
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "dbo.usp_PayrollRun_LoadEmployeePeriodBaseData";
                command.CommandType = CommandType.StoredProcedure;

                AddParameters(command, employeeId, periodStart);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                // Az eljárás első eredményhalmaza gyökéradatokat tartalmaz.
                while (await reader.ReadAsync(cancellationToken))
                {
                    var reference = reader.GetString(
                        reader.GetOrdinal("Reference"));
                    var dataType = reader.GetString(
                        reader.GetOrdinal("DataType"));
                    var value = ReadNullableString(reader, "Value");

                    data[reference] = ParseValue(value, dataType);
                }

                // A többi eredményhalmaz gyűjtemény.
                while (await reader.NextResultAsync(cancellationToken))
                {
                    await ReadCollectionRowsAsync(
                        reader,
                        collections,
                        data,
                        cancellationToken);
                }
            }

            // Kiegészítő adatok: munkarendek, távollétek, bérelemek.
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "dbo.usp_PayrollRun_LoadEmployeePeriodExtendedData";
                command.CommandType = CommandType.StoredProcedure;

                AddParameters(command, employeeId, periodStart);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                await ReadCollectionResultSetsAsync(
                    reader,
                    collections,
                    data,
                    cancellationToken);
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        foreach (var collection in collections)
        {
            data[collection.Key] = collection.Value.Values
                .Cast<object?>()
                .ToArray();
        }

        return data;
    }

    private static async Task ReadCollectionResultSetsAsync(
        DbDataReader reader,
        Dictionary<
            string,
            Dictionary<string, Dictionary<string, object?>>> collections,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        do
        {
            await ReadCollectionRowsAsync(
                reader,
                collections,
                data,
                cancellationToken);
        }
        while (await reader.NextResultAsync(cancellationToken));
    }

    private static async Task ReadCollectionRowsAsync(
        DbDataReader reader,
        Dictionary<
            string,
            Dictionary<string, Dictionary<string, object?>>> collections,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken))
        {
            var collectionReference = reader.GetString(
                reader.GetOrdinal("CollectionReference"));

            var rowKey = reader.GetString(
                reader.GetOrdinal("RowKey"));

            var reference = reader.GetString(
                reader.GetOrdinal("Reference"));

            var dataType = reader.GetString(
                reader.GetOrdinal("DataType"));

            var rawValue = ReadNullableString(reader, "Value");
            var value = ParseValue(rawValue, dataType);

            if (!collections.TryGetValue(
                    collectionReference,
                    out var rows))
            {
                rows = new Dictionary<string, Dictionary<string, object?>>(
                    StringComparer.OrdinalIgnoreCase);
                collections.Add(collectionReference, rows);
            }

            if (!rows.TryGetValue(rowKey, out var row))
            {
                row = new Dictionary<string, object?>(
                    StringComparer.OrdinalIgnoreCase);
                rows.Add(rowKey, row);
            }

            row[reference] = value;

            if (collectionReference.EndsWith(
                    ".customFields",
                    StringComparison.OrdinalIgnoreCase))
            {
                data[reference] = value;
            }
        }
    }

    private static void AddParameters(
        DbCommand command,
        int employeeId,
        DateOnly periodStart)
    {
        AddParameter(
            command,
            "@EmployeeId",
            employeeId,
            DbType.Int32);

        AddParameter(
            command,
            "@PeriodStart",
            periodStart.ToDateTime(TimeOnly.MinValue),
            DbType.Date);
    }

    private static object? ParseValue(string? value, string dataType)
    {
        if (value is null)
        {
            return null;
        }

        return dataType.ToLowerInvariant() switch
        {
            "integer" => int.Parse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture),

            "decimal" => decimal.Parse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture),

            "boolean" => ParseBoolean(value),

            "date" => DateOnly.Parse(
                value,
                CultureInfo.InvariantCulture),

            "time" => TimeSpan.Parse(
                value,
                CultureInfo.InvariantCulture),

            "datetime" => DateTime.Parse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),

            "guid" => Guid.Parse(value),

            _ => value
        };
    }

    private static bool ParseBoolean(string value)
    {
        if (value == "1")
        {
            return true;
        }

        if (value == "0")
        {
            return false;
        }

        return bool.Parse(value);
    }

    private static string? ReadNullableString(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(
                reader.GetValue(ordinal),
                CultureInfo.InvariantCulture);
    }

    private static void AddParameter(
        DbCommand command,
        string parameterName,
        object value,
        DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}