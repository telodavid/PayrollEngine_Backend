using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.WorkScheduleConfigApp;

public sealed class DayTypeRepository(PayrollDbContext database)
{
    public Task<int> CreateAsync(
        DayTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_DayType_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Code", request.Code);
                AddParameter(command, "@Name", request.Name);
                AddParameter(command, "@IsWorkday", request.IsWorkday);
                AddParameter(command, "@StartTime", request.StartTime);
                AddParameter(command, "@EndTime", request.EndTime);
                AddParameter(command, "@BreakMinutes", request.BreakMinutes);

                var result = await command.ExecuteScalarAsync(token);
                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task UpdateAsync(
        int dayTypeId,
        DayTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_DayType_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@DayTypeId", dayTypeId);
                AddParameter(command, "@Code", request.Code);
                AddParameter(command, "@Name", request.Name);
                AddParameter(command, "@IsWorkday", request.IsWorkday);
                AddParameter(command, "@StartTime", request.StartTime);
                AddParameter(command, "@EndTime", request.EndTime);
                AddParameter(command, "@BreakMinutes", request.BreakMinutes);

                await command.ExecuteNonQueryAsync(token);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<DayTypeModel>> SearchAsync(
        string? search,
        bool? isWorkday,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<DayTypeModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_DayType_Search";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Search", search);
                AddParameter(command, "@IsWorkday", isWorkday);

                var result = new List<DayTypeModel>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(MapDayType(reader));
                }

                return result;
            },
            cancellationToken);
    }

    public Task<DayTypeModel?> GetByIdAsync(
        int dayTypeId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<DayTypeModel?>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_DayType_GetById";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@DayTypeId", dayTypeId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                return MapDayType(reader);
            },
            cancellationToken);
    }

    public Task DeleteAsync(
        int dayTypeId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_DayType_Delete";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@DayTypeId", dayTypeId);

                await command.ExecuteNonQueryAsync(token);
            },
            cancellationToken);
    }

    private async Task WithConnectionAsync(
        Func<DbConnection, CancellationToken, Task> operation,
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
            await operation(connection, cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
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

    private static DayTypeModel MapDayType(DbDataReader reader)
    {
        return new DayTypeModel(
            reader.GetInt32(reader.GetOrdinal("DayTypeId")),
            reader.GetString(reader.GetOrdinal("Code")),
            reader.GetString(reader.GetOrdinal("Name")),
            reader.GetBoolean(reader.GetOrdinal("IsWorkday")),
            ReadNullableTime(reader, "StartTime"),
            ReadNullableTime(reader, "EndTime"),
            reader.GetInt16(reader.GetOrdinal("BreakMinutes")));
    }

    private static TimeSpan? ReadNullableTime(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : (TimeSpan)reader.GetValue(ordinal);
    }
}