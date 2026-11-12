using System.Data;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeBankAccountRepository(PayrollDbContext database)
{
    public async Task<EmployeeBankAccountChangeResult> SetAsync(
        int employeeId,
        SetEmployeeBankAccountRequest request,
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
            command.CommandText = "dbo.usp_EmployeeBankAccount_Set";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);
            AddParameter(command, "@AccountType", request.AccountType);
            AddParameter(command, "@AccountIdentifier", request.AccountIdentifier);
            AddParameter(
                command,
                "@ValidFrom",
                request.ValidFrom.ToDateTime(TimeOnly.MinValue));

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A bankszámla-adatokat mentő eljárás nem adott vissza eredményt.");
            }

            return new EmployeeBankAccountChangeResult(
                reader.GetInt32(
                    reader.GetOrdinal("EmployeeBankAccountId")),
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                reader.GetString(reader.GetOrdinal("AccountType")),
                reader.GetString(reader.GetOrdinal("AccountIdentifier")),
                DateOnly.FromDateTime(
                    reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                ReadNullableDate(reader, "ValidTo"));
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
        System.Data.Common.DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : DateOnly.FromDateTime(reader.GetDateTime(ordinal));
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task<EmployeeBankAccountDeleteResult> DeleteAsync(
    int employeeId,
    int employeeBankAccountId,
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
            command.CommandText = "dbo.usp_EmployeeBankAccount_Delete";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);
            AddParameter(
                command,
                "@EmployeeBankAccountId",
                employeeBankAccountId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A bankszámlatörlő eljárás nem adott vissza eredményt.");
            }

            return new EmployeeBankAccountDeleteResult(
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                reader.GetInt32(
                    reader.GetOrdinal("EmployeeBankAccountId")));
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }


    public async Task<IReadOnlyList<EmployeeBankAccountListItem>>

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
                "dbo.usp_EmployeeBankAccount_GetByEmployeeId";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);

            var result = new List<EmployeeBankAccountListItem>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new EmployeeBankAccountListItem(
                    reader.GetInt32(
                        reader.GetOrdinal("EmployeeBankAccountId")),
                    reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                    reader.GetString(reader.GetOrdinal("AccountType")),
                    reader.GetString(reader.GetOrdinal("AccountIdentifier")),
                    DateOnly.FromDateTime(
                        reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                    ReadNullableDate(reader, "ValidTo")));
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