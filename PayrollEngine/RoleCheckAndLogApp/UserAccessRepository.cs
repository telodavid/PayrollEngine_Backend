using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.RoleCheckAndLogApp;

public sealed class UserAccessRepository(PayrollDbContext database)
{
    public async Task<UserAccess> GetUserAccessAsync(
        int userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var roles = await ExecuteQueryAsync(
            "dbo.usp_RoleToUser_GetByUserId",
            userId,
            MapRole,
            cancellationToken);

        var apps = await ExecuteQueryAsync(
            "dbo.usp_User_GetApps",
            userId,
            MapApp,
            cancellationToken);

        return new UserAccess(userId, userName, roles, apps);
    }

    private async Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(
        string procedureName,
        int userId,
        Func<DbDataReader, T> map,
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
            await using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@UserId";
            parameter.Value = userId;
            command.Parameters.Add(parameter);

            var rows = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(map(reader));
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(map(reader));
            }

            return rows;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static UserRole MapRole(DbDataReader reader)
    {
        var validFrom = reader.GetDateTime(reader.GetOrdinal("ValidFrom"));
        var validToOrdinal = reader.GetOrdinal("ValidTo");

        DateOnly? validTo = reader.IsDBNull(validToOrdinal)
            ? null
            : DateOnly.FromDateTime(reader.GetDateTime(validToOrdinal));

        return new UserRole(
            reader.GetInt32(reader.GetOrdinal("RoleId")),
            reader.GetString(reader.GetOrdinal("Code")),
            reader.GetString(reader.GetOrdinal("RoleName")),
            DateOnly.FromDateTime(validFrom),
            validTo);
    }

    private static UserApp MapApp(DbDataReader reader)
        => new(
            reader.GetInt32(reader.GetOrdinal("AppId")),
            reader.GetString(reader.GetOrdinal("Code")),
            reader.GetString(reader.GetOrdinal("AppName")));
}
