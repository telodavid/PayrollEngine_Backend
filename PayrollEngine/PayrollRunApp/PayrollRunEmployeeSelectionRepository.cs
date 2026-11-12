using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayrollEngine.PayrollRunApp.Models;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollRunEmployeeSelectionRepository(
    PayrollDbContext database)
{
    public async Task<IReadOnlyList<PayrollRunEmployeeSelection>> SelectAsync(
        PayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var periodFrom = ParsePeriod(request.PeriodFrom, nameof(request.PeriodFrom));
        var periodTo = ParsePeriod(request.PeriodTo, nameof(request.PeriodTo));

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_PayrollRun_SelectEmployees";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(
                command,
                "@PeriodFrom",
                periodFrom.ToDateTime(TimeOnly.MinValue),
                DbType.Date);

            AddParameter(
                command,
                "@PeriodTo",
                periodTo.ToDateTime(TimeOnly.MinValue),
                DbType.Date);

            var employeeIdsJson = request.EmployeeIds is null
                ? null
                : JsonSerializer.Serialize(request.EmployeeIds);

            AddParameter(
                command,
                "@EmployeeIdsJson",
                employeeIdsJson,
                DbType.String,
                size: -1);

            AddParameter(command, "@EmployeeIdFrom", request.EmployeeIdFrom);
            AddParameter(command, "@EmployeeIdTo", request.EmployeeIdTo);
            AddParameter(command, "@CompanyId", request.CompanyId);
            AddParameter(command, "@IncludeInactive", request.IncludeInactive);

            var employees = new List<PayrollRunEmployeeSelection>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var employeeIdOrdinal = reader.GetOrdinal("EmployeeId");
            var firstNameOrdinal = reader.GetOrdinal("FirstName");
            var lastNameOrdinal = reader.GetOrdinal("LastName");
            var isActiveOrdinal = reader.GetOrdinal("IsActive");
            var effectivePeriodFromOrdinal =
                reader.GetOrdinal("EffectivePeriodFrom");

            while (await reader.ReadAsync(cancellationToken))
            {
                employees.Add(new PayrollRunEmployeeSelection(
                    reader.GetInt32(employeeIdOrdinal),
                    reader.GetString(firstNameOrdinal),
                    reader.GetString(lastNameOrdinal),
                    reader.GetBoolean(isActiveOrdinal),
                    ReadNullableDate(reader, "JoinDate"),
                    ReadNullableDate(reader, "PayrollRunFrom"),
                    DateOnly.FromDateTime(
                        reader.GetDateTime(effectivePeriodFromOrdinal)),
                    ReadNullableDate(reader, "TerminationDate")));
            }

            return employees;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DateOnly ParsePeriod(string value, string parameterName)
    {
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var period))
        {
            throw new ArgumentException(
                "A periódust yyyy-MM formátumban kell megadni, például: 2026-06.",
                parameterName);
        }

        return new DateOnly(period.Year, period.Month, 1);
    }

    private static void AddParameter(
        DbCommand command,
        string parameterName,
        object? value,
        DbType? dbType = null,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;

        if (dbType.HasValue)
        {
            parameter.DbType = dbType.Value;
        }

        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static DateOnly? ReadNullableDate(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : DateOnly.FromDateTime(reader.GetDateTime(ordinal));
    }
}