using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.WorkScheduleConfigApp;

public sealed class EmployeeWorkScheduleChangeRepository(
    PayrollDbContext database)
{
    public async Task<EmployeeWorkScheduleDeleteResult> DeleteAsync(
    int employeeId,
    int employmentWorkScheduleId,
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
            command.CommandText = "dbo.usp_EmployeeWorkSchedule_Delete";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(
                command,
                "@EmployeeId",
                DbType.Int32,
                employeeId);

            AddParameter(
                command,
                "@EmploymentWorkScheduleId",
                DbType.Int32,
                employmentWorkScheduleId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A munkarend-hozzárendelést törlő eljárás nem adott vissza eredményt.");
            }

            return new EmployeeWorkScheduleDeleteResult(
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                reader.GetInt32(
                    reader.GetOrdinal("EmploymentWorkScheduleId")));
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
    public async Task<List<EmployeeWorkScheduleAssignmentListItem>> ListAsync(
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
            command.CommandText = "dbo.usp_EmployeeWorkSchedule_List";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);

            var result = new List<EmployeeWorkScheduleAssignmentListItem>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new EmployeeWorkScheduleAssignmentListItem(
                    EmploymentWorkScheduleId:
                        reader.GetInt32(reader.GetOrdinal("EmploymentWorkScheduleId")),
                    WorkScheduleId:
                        reader.GetInt32(reader.GetOrdinal("WorkScheduleId")),
                    WorkScheduleName:
                        reader.GetString(reader.GetOrdinal("WorkScheduleName")),
                    PeriodLengthDays:
                        reader.GetInt32(reader.GetOrdinal("PeriodLengthDays")),
                    ValidFrom:
                        DateOnly.FromDateTime(
                            reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                    ValidTo:
                        ReadNullableDate(reader, "ValidTo"),
                    CycleStartDate:
                        DateOnly.FromDateTime(
                            reader.GetDateTime(reader.GetOrdinal("CycleStartDate")))));
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
    public async Task<EmployeeWorkScheduleChangeResult> ChangeAsync(
        int employeeId,
        ChangeEmployeeWorkScheduleRequest request,
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
            command.CommandText = "dbo.usp_EmployeeWorkSchedule_Change";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
            AddParameter(command, "@WorkScheduleId", DbType.Int32, request.WorkScheduleId);
            AddParameter(command, "@ValidFrom", DbType.Date, ToDateTime(request.ValidFrom));
            AddParameter(command, "@CycleStartDate", DbType.Date, ToDateTime(request.CycleStartDate));
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
                    "A munkarend-módosító eljárás nem adott vissza eredményt.");
            }

            var requiresRetroPayroll =
                reader.GetBoolean(reader.GetOrdinal("RequiresRetroPayroll"));

            var message = reader.GetString(reader.GetOrdinal("Message"));

            if (requiresRetroPayroll)
            {
                return new EmployeeWorkScheduleChangeResult(
                    RequiresRetroPayroll: true,
                    SuggestedPayrollRunFrom: ReadNullableDate(
                        reader,
                        "SuggestedPayrollRunFrom"),
                    EmployeeId: null,
                    WorkScheduleId: null,
                    ValidFrom: null,
                    CycleStartDate: null,
                    Message: message);
            }

            return new EmployeeWorkScheduleChangeResult(
                RequiresRetroPayroll: false,
                SuggestedPayrollRunFrom: null,
                EmployeeId: reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                WorkScheduleId: reader.GetInt32(reader.GetOrdinal("WorkScheduleId")),
                ValidFrom: ReadNullableDate(reader, "ValidFrom"),
                CycleStartDate: ReadNullableDate(reader, "CycleStartDate"),
                Message: message);
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
}