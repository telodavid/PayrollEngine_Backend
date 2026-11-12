using System.Data;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeTerminationRepository(PayrollDbContext database)
{
    public async Task<EmployeeTerminationResult> TerminateAsync(
        int employeeId,
        DateOnly terminationDate,
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
            command.CommandText = "dbo.usp_Employee_Terminate";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
            AddParameter(
                command,
                "@TerminationDate",
                DbType.Date,
                terminationDate.ToDateTime(TimeOnly.MinValue));

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A kiléptető eljárás nem adott vissza eredményt.");
            }

            return new EmployeeTerminationResult(
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                DateOnly.FromDateTime(
                    reader.GetDateTime(reader.GetOrdinal("TerminationDate"))),
                reader.GetBoolean(reader.GetOrdinal("IsActive")),
                reader.GetInt32(
                    reader.GetOrdinal("ClosedPayrollElements")),
                reader.GetInt32(
                    reader.GetOrdinal("DeletedFuturePayrollElements")));
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
        System.Data.Common.DbCommand command,
        string name,
        DbType dbType,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed record EmployeeTerminationResult(
    int EmployeeId,
    DateOnly TerminationDate,
    bool IsActive,
    int ClosedPayrollElements,
    int DeletedFuturePayrollElements);