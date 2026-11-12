using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayrollEngine.CalculationRuleMaintenanceApp.Execution;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollRunResultRepository(PayrollDbContext database)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<DateOnly> SaveEmployeePeriodResultsAsync(
        Guid runId,
        int employeeId,
        DateOnly periodStart,
        int runByUserId,
        IReadOnlyList<CalculatedPayrollElement> results,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "A futásazonosító nem lehet üres.",
                nameof(runId));
        }

        if (employeeId <= 0 || runByUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(employeeId),
                "A dolgozó- és felhasználó-azonosítónak pozitívnak kell lennie.");
        }

        if (periodStart.Day != 1)
        {
            throw new ArgumentException(
                "A periódus kezdete csak a hónap első napja lehet.",
                nameof(periodStart));
        }

        ArgumentNullException.ThrowIfNull(results);

        var resultsJson = JsonSerializer.Serialize(
            results.Select(result => new
            {
                result.PayrollElementCode,
                result.Amount
            }),
            JsonOptions);

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "dbo.usp_PayrollRun_SaveEmployeePeriodResults";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@RunId", runId, DbType.Guid);
            AddParameter(command, "@EmployeeId", employeeId, DbType.Int32);

            AddParameter(
                command,
                "@Period",
                periodStart.ToString("yyyyMM"),
                DbType.AnsiStringFixedLength,
                size: 6);

            AddParameter(command, "@RunByUserId", runByUserId, DbType.Int32);

            AddParameter(
                command,
                "@ResultsJson",
                resultsJson,
                DbType.String,
                size: -1);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A számfejtési eredmény mentése nem adott vissza eredményt.");
            }

            var nextPeriodOrdinal = reader.GetOrdinal("NextPayrollRunFrom");

            return DateOnly.FromDateTime(
                reader.GetDateTime(nextPeriodOrdinal));
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value,
        DbType dbType,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;

        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
