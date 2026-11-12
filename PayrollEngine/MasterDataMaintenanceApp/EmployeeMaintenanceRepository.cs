using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeMaintenanceRepository(PayrollDbContext database)
{
    public async Task UpdateAsync(
        int employeeId,
        UpdateEmployeeRequest request,
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
            command.CommandText = "dbo.usp_Employee_Update";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", DbType.Int32, employeeId);
            AddParameter(command, "@FirstName", DbType.String, request.FirstName, 100);
            AddParameter(command, "@LastName", DbType.String, request.LastName, 100);
            AddParameter(
                command,
                "@BirthDate",
                DbType.Date,
                ToDatabaseDate(request.BirthDate));
            AddParameter(command, "@PersonalId", DbType.String, request.PersonalId, 30);
            AddParameter(command, "@TaxId", DbType.String, request.TaxId, 30);
            AddParameter(command, "@TAJId", DbType.String, request.TAJId, 30);
            AddParameter(command, "@Address", DbType.String, request.Address, 500);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DateTime? ToDatabaseDate(DateOnly? value)
        => value?.ToDateTime(TimeOnly.MinValue);

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
}