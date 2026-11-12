using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeAbsenceRepository(PayrollDbContext database)
{
    public Task<IReadOnlyList<EmployeeAbsenceRecord>> CreateAsync(
        int employeeId,
        CreateEmployeeAbsenceRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<EmployeeAbsenceRecord>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_EmployeeAbsence_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
                AddParameter(
                    command,
                    "@AbsenceTypeId",
                    DbType.Int32,
                    request.AbsenceTypeId);
                AddParameter(
                    command,
                    "@StartDate",
                    DbType.Date,
                    ToDateTime(request.StartDate));
                AddParameter(
                    command,
                    "@EndDate",
                    DbType.Date,
                    ToDateTime(request.EndDate));
                AddParameter(
                    command,
                    "@FullDay",
                    DbType.Boolean,
                    request.FullDay);
                AddParameter(
                    command,
                    "@StartTime",
                    DbType.Time,
                    request.StartTime?.ToTimeSpan());
                AddParameter(
                    command,
                    "@EndTime",
                    DbType.Time,
                    request.EndTime?.ToTimeSpan());

                var records = new List<EmployeeAbsenceRecord>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    records.Add(MapAbsence(reader));
                }

                return records;
            },
            cancellationToken);
    }

    public Task<EmployeeAbsenceRecord> DeleteAsync(
        int employeeId,
        int absenceId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_EmployeeAbsence_Delete";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
                AddParameter(command, "@AbsenceId", DbType.Int32, absenceId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException(
                        "A távolléttörlő eljárás nem adott vissza eredményt.");
                }

                return MapAbsence(reader);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<EmployeeAbsenceListItem>> GetByEmployeeIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<EmployeeAbsenceListItem>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "dbo.usp_EmployeeAbsence_GetByEmployeeId";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);

                var records = new List<EmployeeAbsenceListItem>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    records.Add(MapAbsenceListItem(reader));
                }

                return records;
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
        DbType type,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static EmployeeAbsenceRecord MapAbsence(DbDataReader reader)
    {
        return new EmployeeAbsenceRecord(
            reader.GetInt32(reader.GetOrdinal("AbsenceId")),
            reader.GetInt32(reader.GetOrdinal("EmploymentId")),
            reader.GetInt32(reader.GetOrdinal("AbsenceTypeId")),
            DateOnly.FromDateTime(
                reader.GetDateTime(reader.GetOrdinal("StartDate"))),
            DateOnly.FromDateTime(
                reader.GetDateTime(reader.GetOrdinal("EndDate"))),
            reader.GetBoolean(reader.GetOrdinal("FullDay")),
            ReadNullableTime(reader, "StartTime"),
            ReadNullableTime(reader, "EndTime"));
    }

    private static EmployeeAbsenceListItem MapAbsenceListItem(
        DbDataReader reader)
    {
        return new EmployeeAbsenceListItem(
            reader.GetInt32(reader.GetOrdinal("EmployeeId")),
            reader.GetInt32(reader.GetOrdinal("AbsenceId")),
            reader.GetInt32(reader.GetOrdinal("EmploymentId")),
            reader.GetInt32(reader.GetOrdinal("AbsenceTypeId")),
            reader.GetString(reader.GetOrdinal("AbsenceTypeCode")),
            reader.GetString(reader.GetOrdinal("AbsenceTypeName")),
            reader.GetBoolean(reader.GetOrdinal("IsPaid")),
            DateOnly.FromDateTime(
                reader.GetDateTime(reader.GetOrdinal("StartDate"))),
            DateOnly.FromDateTime(
                reader.GetDateTime(reader.GetOrdinal("EndDate"))),
            reader.GetBoolean(reader.GetOrdinal("FullDay")),
            ReadNullableTime(reader, "StartTime"),
            ReadNullableTime(reader, "EndTime"));
    }

    private static TimeOnly? ReadNullableTime(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : TimeOnly.FromTimeSpan((TimeSpan)reader.GetValue(ordinal));
    }

    private static DateTime ToDateTime(DateOnly date)
        => date.ToDateTime(TimeOnly.MinValue);
}