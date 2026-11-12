using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeOnboardingRepository(PayrollDbContext database)
{
    public async Task<int> OnboardAsync(
        EmployeeOnboardingRequest request,
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
            command.CommandText = "dbo.usp_Employee_Onboard";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@FirstName", DbType.String, request.FirstName, 100);
            AddParameter(command, "@LastName", DbType.String, request.LastName, 100);
            AddParameter(command, "@BirthDate", DbType.Date, ToDatabaseDate(request.BirthDate));
            AddParameter(command, "@PersonalId", DbType.String, request.PersonalId, 30);
            AddParameter(command, "@TaxId", DbType.String, request.TaxId, 30);
            AddParameter(command, "@TAJId", DbType.String, request.TAJId, 30);
            AddParameter(command, "@Address", DbType.String, request.Address, 500);
            AddParameter(command, "@JoinDate", DbType.Date, ToDatabaseDate(request.JoinDate));
            AddParameter(command, "@CompanyId", DbType.Int32, request.CompanyId);
            AddParameter(command, "@WorkScheduleId", DbType.Int32, request.WorkScheduleId);
            AddParameter(command, "@PayrollElementId", DbType.Int32, request.PayrollElementId);

            var salaryParameter = command.CreateParameter();
            salaryParameter.ParameterName = "@BaseSalaryAmount";
            salaryParameter.DbType = DbType.Decimal;
            salaryParameter.Precision = 18;
            salaryParameter.Scale = 2;
            salaryParameter.Value =
                request.BaseSalaryAmount.HasValue
                    ? request.BaseSalaryAmount.Value
                    : DBNull.Value;
            command.Parameters.Add(salaryParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is null or DBNull)
            {
                throw new InvalidOperationException(
                    "A beléptető eljárás nem adott vissza employee-azonosítót.");
            }

            return Convert.ToInt32(result);
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