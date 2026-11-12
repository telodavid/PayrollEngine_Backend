using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmploymentMaintenanceRepository(
    PayrollDbContext database)
{
    public async Task<EmploymentChangeResult> ChangeCompanyAsync(
        int employeeId,
        ChangeEmploymentCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_Employment_ChangeCompany";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
            AddParameter(command, "@CompanyId", DbType.Int32, request.CompanyId);
            AddParameter(
                command,
                "@ValidFrom",
                DbType.Date,
                ToDateTime(request.ValidFrom));
            AddParameter(
                command,
                "@RetroPayrollRunFrom",
                DbType.Date,
                request.RetroPayrollRunFrom.HasValue
                    ? ToDateTime(request.RetroPayrollRunFrom.Value)
                    : DBNull.Value);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A munkaviszony-módosító eljárás nem adott vissza eredményt.");
            }

            var requiresRetroPayroll =
                reader.GetBoolean(reader.GetOrdinal("RequiresRetroPayroll"));

            var result = new EmploymentChangeResult(
                RequiresRetroPayroll: requiresRetroPayroll,
                SuggestedPayrollRunFrom: ReadNullableDate(
                    reader,
                    "SuggestedPayrollRunFrom"),
                EmployeeId: ReadNullableInt(reader, "EmployeeId"),
                EmploymentId: ReadNullableInt(reader, "EmploymentId"),
                CompanyId: ReadNullableInt(reader, "CompanyId"),
                ValidFrom: ReadNullableDate(reader, "ValidFrom"),
                ValidTo: ReadNullableDate(reader, "ValidTo"),
                Message: reader.GetString(reader.GetOrdinal("Message")));

            return result;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<EmploymentDeleteResult> DeleteAsync(
    int employeeId,
    int employmentId,
    CancellationToken cancellationToken = default)
    {
        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_Employment_Delete";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
            AddParameter(command, "@EmploymentId", DbType.Int32, employmentId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A munkaviszonytörlő eljárás nem adott vissza eredményt.");
            }

            return new EmploymentDeleteResult(
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                reader.GetInt32(reader.GetOrdinal("EmploymentId")),
                ReadNullableDate(reader, "PayrollRunFrom"));
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
    public async Task<IReadOnlyList<EmploymentHistoryItem>> GetByEmployeeIdAsync(
    int employeeId,
    CancellationToken cancellationToken = default)
    {
        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_Employment_GetByEmployeeId";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);

            var result = new List<EmploymentHistoryItem>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var employeeIdOrdinal = reader.GetOrdinal("EmployeeId");
            var employmentIdOrdinal = reader.GetOrdinal("EmploymentId");
            var companyIdOrdinal = reader.GetOrdinal("CompanyId");
            var companyNameOrdinal = reader.GetOrdinal("CompanyName");
            var validFromOrdinal = reader.GetOrdinal("ValidFrom");
            var validToOrdinal = reader.GetOrdinal("ValidTo");

            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new EmploymentHistoryItem(
                    reader.GetInt32(employeeIdOrdinal),
                    reader.GetInt32(employmentIdOrdinal),
                    reader.GetInt32(companyIdOrdinal),
                    reader.GetString(companyNameOrdinal),
                    DateOnly.FromDateTime(reader.GetDateTime(validFromOrdinal)),
                    reader.IsDBNull(validToOrdinal)
                        ? null
                        : DateOnly.FromDateTime(reader.GetDateTime(validToOrdinal))));
            }

            return result;
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
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static DateTime ToDateTime(DateOnly date)
        => date.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? ReadNullableDate(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : DateOnly.FromDateTime(reader.GetDateTime(ordinal));
    }

    private static int? ReadNullableInt(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt32(ordinal);
    }
}