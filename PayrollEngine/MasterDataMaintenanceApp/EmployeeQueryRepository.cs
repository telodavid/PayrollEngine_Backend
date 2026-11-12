using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeQueryRepository(PayrollDbContext database)
{
    public Task<IReadOnlyList<EmployeeListItem>> SearchAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<EmployeeListItem>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Employee_Search";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Search", DbType.String, search, 100);
                AddParameter(command, "@IsActive", DbType.Boolean, isActive);

                var result = new List<EmployeeListItem>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(new EmployeeListItem(
                        reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                        reader.GetString(reader.GetOrdinal("FirstName")),
                        reader.GetString(reader.GetOrdinal("LastName")),
                        reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        ReadNullableDate(reader, "JoinDate"),
                        ReadNullableDate(reader, "PayrollRunFrom")));
                }

                return result;
            },
            cancellationToken);
    }

    public Task<EmployeeDetails?> GetByIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<EmployeeDetails?>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Employee_GetById";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                return new EmployeeDetails(
                    reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                    reader.GetString(reader.GetOrdinal("FirstName")),
                    reader.GetString(reader.GetOrdinal("LastName")),
                    ReadNullableDate(reader, "BirthDate"),
                    reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    ReadNullableString(reader, "PersonalId"),
                    ReadNullableString(reader, "TaxId"),
                    ReadNullableString(reader, "TAJId"),
                    ReadNullableString(reader, "Address"),
                    ReadNullableDate(reader, "PayrollRunFrom"),
                    ReadNullableDate(reader, "JoinDate"));
            },
            cancellationToken);
    }

    private async Task<T> WithConnectionAsync<T>(
        Func<DbConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await operation(connection, cancellationToken);
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
        DbType dbType,
        object? value,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;

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

    private static string? ReadNullableString(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }
}