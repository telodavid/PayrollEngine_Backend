using System.Data;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.UserMaintenanceApp;

public sealed record UserSummary(
    int UserId,
    string UserName,
    bool IsActive);

public sealed class UserMaintenanceRepository(PayrollDbContext database)
{
    public async Task<IReadOnlyList<UserSummary>> SearchUsersAsync(
    string? search = null,
    bool? isActive = null,
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
            command.CommandText = "dbo.usp_User_Search";
            command.CommandType = CommandType.StoredProcedure;

            var searchParameter = command.CreateParameter();
            searchParameter.ParameterName = "@Search";
            searchParameter.Value = string.IsNullOrWhiteSpace(search)
                ? DBNull.Value
                : search.Trim();
            command.Parameters.Add(searchParameter);

            var activeParameter = command.CreateParameter();
            activeParameter.ParameterName = "@IsActive";
            activeParameter.Value = isActive.HasValue
                ? (object)isActive.Value
                : DBNull.Value;
            command.Parameters.Add(activeParameter);

            var users = new List<UserSummary>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new UserSummary(
                    reader.GetInt32(reader.GetOrdinal("UserId")),
                    reader.GetString(reader.GetOrdinal("UserName")),
                    reader.GetBoolean(reader.GetOrdinal("IsActive"))));
            }

            return users;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
    public async Task<int> CreateUserAsync(
    string userName,
    string password,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException(
                "A felhasználónév nem lehet üres.",
                nameof(userName));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException(
                "A jelszó nem lehet üres.",
                nameof(password));
        }

        if (password.Length > 255)
        {
            throw new ArgumentException(
                "A jelszó legfeljebb 255 karakter lehet.",
                nameof(password));
        }

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_User_Create";
            command.CommandType = CommandType.StoredProcedure;

            var userNameParameter = command.CreateParameter();
            userNameParameter.ParameterName = "@UserName";
            userNameParameter.Value = userName;
            command.Parameters.Add(userNameParameter);

            var passwordParameter = command.CreateParameter();
            passwordParameter.ParameterName = "@PasswordHash";
            passwordParameter.Value = password;
            command.Parameters.Add(passwordParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is null or DBNull)
            {
                throw new InvalidOperationException(
                    "A felhasználó létrejött, de az eljárás nem adott vissza azonosítót.");
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
    public async Task SetPasswordAsync(
        int userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new ArgumentException(
                "A jelszó nem lehet üres.",
                nameof(newPassword));
        }

        if (newPassword.Length > 255)
        {
            throw new ArgumentException(
                "A jelszó legfeljebb 255 karakter lehet.",
                nameof(newPassword));
        }

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_User_SetPassword";
            command.CommandType = CommandType.StoredProcedure;

            var userIdParameter = command.CreateParameter();
            userIdParameter.ParameterName = "@UserId";
            userIdParameter.Value = userId;
            command.Parameters.Add(userIdParameter);

            var passwordParameter = command.CreateParameter();
            passwordParameter.ParameterName = "@NewPassword";
            passwordParameter.Value = newPassword;
            command.Parameters.Add(passwordParameter);

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
    public async Task UpdateUserAsync(
    int userId,
    string userName,
    bool isActive,
    int currentUserId,
    CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        if (currentUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentUserId));
        }

        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 100)
        {
            throw new ArgumentException(
                "A felhasználónév nem lehet üres, és legfeljebb 100 karakter lehet.",
                nameof(userName));
        }

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_User_Update";
            command.CommandType = CommandType.StoredProcedure;

            var userIdParameter = command.CreateParameter();
            userIdParameter.ParameterName = "@UserId";
            userIdParameter.Value = userId;
            command.Parameters.Add(userIdParameter);

            var userNameParameter = command.CreateParameter();
            userNameParameter.ParameterName = "@UserName";
            userNameParameter.Value = userName.Trim();
            command.Parameters.Add(userNameParameter);

            var activeParameter = command.CreateParameter();
            activeParameter.ParameterName = "@IsActive";
            activeParameter.Value = isActive;
            command.Parameters.Add(activeParameter);

            var currentUserIdParameter = command.CreateParameter();
            currentUserIdParameter.ParameterName = "@CurrentUserId";
            currentUserIdParameter.Value = currentUserId;
            command.Parameters.Add(currentUserIdParameter);

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
}