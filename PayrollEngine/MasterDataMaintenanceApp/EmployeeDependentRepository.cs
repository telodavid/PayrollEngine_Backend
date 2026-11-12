using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeDependentRepository(PayrollDbContext database)
{
    public Task<IReadOnlyList<EmployeeDependentItem>> GetByEmployeeIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
        => WithConnectionAsync<IReadOnlyList<EmployeeDependentItem>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "dbo.usp_EmployeeDependent_GetByEmployeeId";
                command.CommandType = CommandType.StoredProcedure;
                AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);

                var result = new List<EmployeeDependentItem>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(new EmployeeDependentItem(
                        reader.GetInt32(reader.GetOrdinal("EmployeeDependentId")),
                        reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                        reader.GetInt32(reader.GetOrdinal("DependentId")),
                        reader.GetString(reader.GetOrdinal("FirstName")),
                        reader.GetString(reader.GetOrdinal("LastName")),
                        ReadNullableDate(reader, "BirthDate"),
                        ReadNullableString(reader, "TaxId"),
                        reader.GetInt32(reader.GetOrdinal("RelationshipTypeId")),
                        reader.GetString(reader.GetOrdinal("RelationshipTypeName")),
                        ReadNullableDate(reader, "ValidFrom"),
                        ReadNullableDate(reader, "ValidTo")));
                }

                return result;
            },
            cancellationToken);

    public Task<IReadOnlyList<RelationshipTypeItem>> GetRelationshipTypesAsync(
        CancellationToken cancellationToken = default)
        => WithConnectionAsync<IReadOnlyList<RelationshipTypeItem>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_RelationshipType_GetAll";
                command.CommandType = CommandType.StoredProcedure;

                var result = new List<RelationshipTypeItem>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(new RelationshipTypeItem(
                        reader.GetInt32(reader.GetOrdinal("RelationshipTypeId")),
                        reader.GetString(reader.GetOrdinal("Name"))));
                }

                return result;
            },
            cancellationToken);

    public Task<EmployeeDependentSaveResult> CreateAsync(
        int employeeId,
        SaveEmployeeDependentRequest request,
        CancellationToken cancellationToken = default)
        => SaveAsync(
            "dbo.usp_EmployeeDependent_Create",
            employeeId,
            null,
            request,
            cancellationToken);

    public Task<EmployeeDependentSaveResult> UpdateAsync(
        int employeeId,
        int employeeDependentId,
        SaveEmployeeDependentRequest request,
        CancellationToken cancellationToken = default)
        => SaveAsync(
            "dbo.usp_EmployeeDependent_Update",
            employeeId,
            employeeDependentId,
            request,
            cancellationToken);

    public Task DeleteAsync(
        int employeeId,
        int employeeDependentId,
        CancellationToken cancellationToken = default)
        => WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_EmployeeDependent_Delete";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
                AddParameter(
                    command,
                    "@EmployeeDependentId",
                    DbType.Int32,
                    employeeDependentId);

                await command.ExecuteNonQueryAsync(token);
                return true;
            },
            cancellationToken);

    private Task<EmployeeDependentSaveResult> SaveAsync(
        string procedure,
        int employeeId,
        int? employeeDependentId,
        SaveEmployeeDependentRequest request,
        CancellationToken cancellationToken)
        => WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = procedure;
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);

                if (employeeDependentId.HasValue)
                {
                    AddParameter(
                        command,
                        "@EmployeeDependentId",
                        DbType.Int32,
                        employeeDependentId.Value);
                }

                AddParameter(command, "@FirstName", DbType.String, request.FirstName);
                AddParameter(command, "@LastName", DbType.String, request.LastName);
                AddParameter(
                    command,
                    "@BirthDate",
                    DbType.Date,
                    ToDateTime(request.BirthDate));
                AddParameter(command, "@TaxId", DbType.String, request.TaxId);
                AddParameter(
                    command,
                    "@RelationshipTypeId",
                    DbType.Int32,
                    request.RelationshipTypeId);
                AddParameter(
                    command,
                    "@ValidFrom",
                    DbType.Date,
                    ToDateTime(request.ValidFrom));
                AddParameter(
                    command,
                    "@ValidTo",
                    DbType.Date,
                    ToDateTime(request.ValidTo));

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException(
                        "Az eltartottat mentő tárolt eljárás nem adott vissza eredményt.");
                }

                return new EmployeeDependentSaveResult(
                    reader.GetInt32(reader.GetOrdinal("EmployeeDependentId")),
                    reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                    reader.GetInt32(reader.GetOrdinal("DependentId")),
                    reader.GetString(reader.GetOrdinal("FirstName")),
                    reader.GetString(reader.GetOrdinal("LastName")),
                    ReadNullableDate(reader, "BirthDate"),
                    ReadNullableString(reader, "TaxId"),
                    reader.GetInt32(reader.GetOrdinal("RelationshipTypeId")),
                    reader.GetString(reader.GetOrdinal("RelationshipTypeName")),
                    ReadNullableDate(reader, "ValidFrom"),
                    ReadNullableDate(reader, "ValidTo"));
            },
            cancellationToken);

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

    private static DateTime? ToDateTime(DateOnly? value)
        => value?.ToDateTime(TimeOnly.MinValue);

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}