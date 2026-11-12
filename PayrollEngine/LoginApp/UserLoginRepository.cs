using System.Data;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.LoginApp;

public sealed class UserLoginRepository(PayrollDbContext database)
{
    public async Task<LoginUser?> FindByUserNameAsync(
        string userName,
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
            command.CommandText = "dbo.usp_User_GetForLogin";
            command.CommandType = CommandType.StoredProcedure;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@UserName";
            parameter.Value = userName;
            command.Parameters.Add(parameter);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new LoginUser(
                reader.GetInt32(reader.GetOrdinal("UserId")),
                reader.GetString(reader.GetOrdinal("UserName")),
                reader.GetString(reader.GetOrdinal("PasswordHash")),
                reader.GetBoolean(reader.GetOrdinal("IsActive")));
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

public sealed record LoginUser(
    int UserId,
    string UserName,
    string Password,
    bool IsActive);

public sealed record LoginRequest(
    string UserName,
    string Password);