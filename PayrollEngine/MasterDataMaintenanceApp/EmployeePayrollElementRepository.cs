using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeePayrollElementRepository(PayrollDbContext database)
{
    public async Task<EmployeePayrollElementChangeResult> SetAsync(
        int employeeId,
        SetEmployeePayrollElementRequest request,
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
            command.CommandText = "dbo.usp_EmployeePayrollElement_Set";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);
            AddParameter(command, "@EmploymentId", request.EmploymentId);
            AddParameter(command, "@PayrollElementId", request.PayrollElementId);
            AddParameter(
                command,
                "@ValidFrom",
                request.ValidFrom.ToDateTime(TimeOnly.MinValue));
            AddParameter(command, "@Amount", request.Amount);
            AddParameter(command, "@ValidTo", ToDbValue(request.ValidTo));
            AddParameter(
                command,
                "@RetroPayrollRunFrom",
                ToDbValue(request.RetroPayrollRunFrom));

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A bérelem-módosító eljárás nem adott vissza eredményt.");
            }

            return MapResult(reader);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<EmployeePayrollElementDeleteResult> DeleteAsync(
    int employeeId,
    int employmentPayrollElementId,
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
            command.CommandText = "dbo.usp_EmployeePayrollElement_Delete";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);
            AddParameter(
                command,
                "@EmploymentPayrollElementId",
                employmentPayrollElementId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A bérelemtörlő eljárás nem adott vissza eredményt.");
            }

            return new EmployeePayrollElementDeleteResult(
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                reader.GetInt32(
                    reader.GetOrdinal("EmploymentPayrollElementId")),
                DateOnly.FromDateTime(
                    reader.GetDateTime(reader.GetOrdinal("PayrollRunFrom"))));
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static EmployeePayrollElementChangeResult MapResult(
        DbDataReader reader)
    {
        var assignmentIdOrdinal =
            reader.GetOrdinal("EmploymentPayrollElementId");

        return new EmployeePayrollElementChangeResult(
            reader.GetBoolean(reader.GetOrdinal("RequiresRetroPayroll")),
            ReadNullableDate(reader, "SuggestedPayrollRunFrom"),
            reader.IsDBNull(assignmentIdOrdinal)
                ? null
                : reader.GetInt32(assignmentIdOrdinal),
            reader.GetInt32(reader.GetOrdinal("EmployeeId")),
            reader.GetInt32(reader.GetOrdinal("EmploymentId")),
            reader.GetInt32(reader.GetOrdinal("PayrollElementId")),
            DateOnly.FromDateTime(
                reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
            ReadNullableDate(reader, "ValidTo"),
            reader.GetDecimal(reader.GetOrdinal("Amount")),
            reader.GetString(reader.GetOrdinal("Message")));
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

    private static object ToDbValue(DateOnly? value)
    {
        return value.HasValue
            ? value.Value.ToDateTime(TimeOnly.MinValue)
            : DBNull.Value;
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
    public async Task<IReadOnlyList<EmployeePayrollElementListItem>>
    GetByEmployeeIdAsync(
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
            command.CommandText =
                "dbo.usp_EmployeePayrollElement_GetByEmployeeId";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);

            var result = new List<EmployeePayrollElementListItem>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new EmployeePayrollElementListItem(
                    reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                    reader.GetInt32(
                        reader.GetOrdinal("EmploymentPayrollElementId")),
                    reader.GetInt32(reader.GetOrdinal("EmploymentId")),
                    reader.GetInt32(reader.GetOrdinal("CompanyId")),
                    reader.GetString(reader.GetOrdinal("CompanyName")),
                    reader.GetInt32(reader.GetOrdinal("PayrollElementId")),
                    reader.GetString(reader.GetOrdinal("PayrollElementCode")),
                    reader.GetString(reader.GetOrdinal("PayrollElementName")),
                    reader.GetBoolean(
                        reader.GetOrdinal("PayrollElementIsActive")),
                    DateOnly.FromDateTime(
                        reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                    ReadNullableDate(reader, "ValidTo"),
                    reader.GetDecimal(reader.GetOrdinal("Amount"))));
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
}