using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.AbsenceTypeMaintenanceApp;

public sealed class AbsenceTypeRepository(PayrollDbContext database)
{
    public Task<int> CreateAsync(
        CreateAbsenceTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_AbsenceType_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Code", request.Code);
                AddParameter(command, "@Name", request.Name);
                AddParameter(command, "@IsPaid", request.IsPaid);

                var result = await command.ExecuteScalarAsync(token);
                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task UpdateAsync(
        int absenceTypeId,
        UpdateAbsenceTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_AbsenceType_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@AbsenceTypeId", absenceTypeId);
                AddParameter(command, "@Code", request.Code);
                AddParameter(command, "@Name", request.Name);
                AddParameter(command, "@IsPaid", request.IsPaid);
                AddParameter(command, "@IsActive", request.IsActive);

                await command.ExecuteNonQueryAsync(token);
                return true;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<AbsenceTypeModel>> SearchAsync(
        string? search,
        bool? isPaid,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<AbsenceTypeModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_AbsenceType_Search";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Search", search);
                AddParameter(command, "@IsPaid", isPaid);
                AddParameter(command, "@IsActive", isActive);

                var result = new List<AbsenceTypeModel>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(MapAbsenceType(reader));
                }

                return result;
            },
            cancellationToken);
    }

    public Task<AbsenceTypeModel?> GetByIdAsync(
        int absenceTypeId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<AbsenceTypeModel?>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_AbsenceType_GetById";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@AbsenceTypeId", absenceTypeId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                return MapAbsenceType(reader);
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
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static AbsenceTypeModel MapAbsenceType(DbDataReader reader)
    {
        return new AbsenceTypeModel(
            reader.GetInt32(reader.GetOrdinal("AbsenceTypeId")),
            reader.GetString(reader.GetOrdinal("Code")),
            reader.GetString(reader.GetOrdinal("Name")),
            reader.GetBoolean(reader.GetOrdinal("IsPaid")),
            reader.GetBoolean(reader.GetOrdinal("IsActive")));
    }
}