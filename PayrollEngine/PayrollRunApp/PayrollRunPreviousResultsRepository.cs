using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollRunPreviousResultsRepository(
    PayrollDbContext database)
{
    public async Task<IReadOnlyList<PreviousPayrollElementResult>>
        GetPreviousPeriodResultsAsync(
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
                "dbo.usp_PayrollRun_GetPreviousPeriodResults";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(
                command,
                "@EmployeeId",
                employeeId,
                DbType.Int32);

            AddParameter(
                command,
                "@Period",
                periodStart.ToString("yyyyMM"),
                DbType.AnsiStringFixedLength,
                size: 6);

            var results = new List<PreviousPayrollElementResult>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new PreviousPayrollElementResult(
                    reader.GetGuid(reader.GetOrdinal("PreviousRunId")),
                    DateOnly.FromDateTime(
                        reader.GetDateTime(reader.GetOrdinal("Period"))),
                    reader.GetInt32(reader.GetOrdinal("PayrollElementId")),
                    reader.GetString(reader.GetOrdinal("PayrollElementCode")),
                    reader.GetString(reader.GetOrdinal("PayrollElementName")),
                    reader.GetString(reader.GetOrdinal("ResultType")),
                    reader.GetDecimal(reader.GetOrdinal("Amount"))));
            }

            return results;
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

public sealed record PreviousPayrollElementResult(
    Guid PreviousRunId,
    DateOnly Period,
    int PayrollElementId,
    string PayrollElementCode,
    string PayrollElementName,
    string ResultType,
    decimal Amount);