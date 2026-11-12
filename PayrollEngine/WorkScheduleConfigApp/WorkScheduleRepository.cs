using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.WorkScheduleConfigApp;

public sealed class WorkScheduleRepository(PayrollDbContext database)
{
    public Task<int> CreateAsync(
        CreateWorkScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = CreateStoredProcedureCommand(
                    connection,
                    "dbo.usp_WorkSchedule_Create");

                AddParameter(command, "@Name", SqlDbType.NVarChar, request.Name);
                AddParameter(
                    command,
                    "@PeriodLengthDays",
                    SqlDbType.Int,
                    request.PeriodLengthDays);

                command.Parameters.Add(CreateDaysParameter(request.Days));

                var result = await command.ExecuteScalarAsync(token);
                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task UpdateAsync(
        int workScheduleId,
        UpdateWorkScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = CreateStoredProcedureCommand(
                    connection,
                    "dbo.usp_WorkSchedule_Update");

                AddParameter(
                    command,
                    "@WorkScheduleId",
                    SqlDbType.Int,
                    workScheduleId);
                AddParameter(command, "@Name", SqlDbType.NVarChar, request.Name);
                AddParameter(
                    command,
                    "@PeriodLengthDays",
                    SqlDbType.Int,
                    request.PeriodLengthDays);
                AddParameter(
                    command,
                    "@IsActive",
                    SqlDbType.Bit,
                    request.IsActive);

                command.Parameters.Add(CreateDaysParameter(request.Days));

                await command.ExecuteNonQueryAsync(token);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<WorkScheduleListItem>> SearchAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<WorkScheduleListItem>>(
            async (connection, token) =>
            {
                await using var command = CreateStoredProcedureCommand(
                    connection,
                    "dbo.usp_WorkSchedule_Search");

                AddParameter(command, "@Search", SqlDbType.NVarChar, search);
                AddParameter(command, "@IsActive", SqlDbType.Bit, isActive);

                var result = new List<WorkScheduleListItem>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(new WorkScheduleListItem(
                        reader.GetInt32(reader.GetOrdinal("WorkScheduleId")),
                        reader.GetString(reader.GetOrdinal("Name")),
                        reader.GetInt32(reader.GetOrdinal("PeriodLengthDays")),
                        reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        reader.GetInt32(reader.GetOrdinal("ConfiguredDays"))));
                }

                return result;
            },
            cancellationToken);
    }

    public Task<WorkScheduleDetails?> GetByIdAsync(
        int workScheduleId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<WorkScheduleDetails?>(
            async (connection, token) =>
            {
                await using var command = CreateStoredProcedureCommand(
                    connection,
                    "dbo.usp_WorkSchedule_GetById");

                AddParameter(
                    command,
                    "@WorkScheduleId",
                    SqlDbType.Int,
                    workScheduleId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                var scheduleId =
                    reader.GetInt32(reader.GetOrdinal("WorkScheduleId"));
                var name = reader.GetString(reader.GetOrdinal("Name"));
                var periodLengthDays =
                    reader.GetInt32(reader.GetOrdinal("PeriodLengthDays"));
                var isActive =
                    reader.GetBoolean(reader.GetOrdinal("IsActive"));

                var days = new List<WorkScheduleDayModel>();

                if (await reader.NextResultAsync(token))
                {
                    while (await reader.ReadAsync(token))
                    {
                        days.Add(new WorkScheduleDayModel(
                                reader.GetInt32(reader.GetOrdinal("DayOfPeriod")),
                                reader.GetInt32(reader.GetOrdinal("DayTypeId")),
                                reader.GetString(reader.GetOrdinal("DayTypeCode")),
                                reader.GetString(reader.GetOrdinal("DayTypeName")),
                                reader.GetBoolean(reader.GetOrdinal("IsWorkday")),
                                ReadNullableTime(reader, "StartTime"),
                                ReadNullableTime(reader, "EndTime"),
                                reader.GetInt16(reader.GetOrdinal("BreakMinutes"))));
                    }
                }

                return new WorkScheduleDetails(
                    scheduleId,
                    name,
                    periodLengthDays,
                    isActive,
                    days);
            },
            cancellationToken);
    }

    private static SqlCommand CreateStoredProcedureCommand(
        DbConnection connection,
        string procedureName)
    {
        var command = (SqlCommand)connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        return command;
    }

    private static void AddParameter(
        SqlCommand command,
        string name,
        SqlDbType type,
        object? value)
    {
        command.Parameters.Add(new SqlParameter(name, type)
        {
            Value = value ?? DBNull.Value
        });
    }

    private static SqlParameter CreateDaysParameter(
        IReadOnlyList<WorkScheduleDayRequest> days)
    {
        var parameter = new SqlParameter("@Days", SqlDbType.Structured)
        {
            TypeName = "dbo.WorkScheduleDayInputType",
            Value = CreateDaysTable(days)
        };

        return parameter;
    }

    private static DataTable CreateDaysTable(
        IReadOnlyList<WorkScheduleDayRequest> days)
    {
        var table = new DataTable();
        table.Columns.Add("DayOfPeriod", typeof(int));
        table.Columns.Add("DayTypeId", typeof(int));

        foreach (var day in days)
        {
            table.Rows.Add(day.DayOfPeriod, day.DayTypeId);
        }

        return table;
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