using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace PayrollEngine.UserMaintenanceApp;

public sealed class RoleMaintenanceRepository(PayrollDbContext database)
{
    public async Task<int> CreateRoleAsync(
        string code,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "A szerepkód nem lehet üres.",
                nameof(code));
        }

        if (code.Length > 50)
        {
            throw new ArgumentException(
                "A szerepkód legfeljebb 50 karakter lehet.",
                nameof(code));
        }

        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ArgumentException(
                "A szerep neve nem lehet üres.",
                nameof(roleName));
        }

        if (roleName.Length > 100)
        {
            throw new ArgumentException(
                "A szerep neve legfeljebb 100 karakter lehet.",
                nameof(roleName));
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
            command.CommandText = "dbo.usp_Role_Create";
            command.CommandType = CommandType.StoredProcedure;

            var codeParameter = command.CreateParameter();
            codeParameter.ParameterName = "@Code";
            codeParameter.Value = code;
            command.Parameters.Add(codeParameter);

            var roleNameParameter = command.CreateParameter();
            roleNameParameter.ParameterName = "@RoleName";
            roleNameParameter.Value = roleName;
            command.Parameters.Add(roleNameParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is null or DBNull)
            {
                throw new InvalidOperationException(
                    "A szerep létrehozása nem adott vissza azonosítót.");
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
    public async Task<IReadOnlyList<RoleSummary>> GetRolesAsync(
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
            command.CommandText = "dbo.usp_Role_Search";
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

            var roles = new List<RoleSummary>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(new RoleSummary(
                    reader.GetInt32(reader.GetOrdinal("RoleId")),
                    reader.GetString(reader.GetOrdinal("Code")),
                    reader.GetString(reader.GetOrdinal("RoleName")),
                    reader.GetBoolean(reader.GetOrdinal("IsActive"))));
            }

            return roles;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
    public async Task UpdateRoleAsync(
    int roleId,
    string code,
    string roleName,
    bool isActive,
    int currentUserId,
    CancellationToken cancellationToken = default)
    {
        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
        }

        if (currentUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentUserId));
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
        {
            throw new ArgumentException(
                "A szerepkód nem lehet üres, és legfeljebb 50 karakter lehet.",
                nameof(code));
        }

        if (string.IsNullOrWhiteSpace(roleName) || roleName.Length > 100)
        {
            throw new ArgumentException(
                "A szerep neve nem lehet üres, és legfeljebb 100 karakter lehet.",
                nameof(roleName));
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
            command.CommandText = "dbo.usp_Role_Update";
            command.CommandType = CommandType.StoredProcedure;

            var roleIdParameter = command.CreateParameter();
            roleIdParameter.ParameterName = "@RoleId";
            roleIdParameter.Value = roleId;
            command.Parameters.Add(roleIdParameter);

            var codeParameter = command.CreateParameter();
            codeParameter.ParameterName = "@Code";
            codeParameter.Value = code;
            command.Parameters.Add(codeParameter);

            var roleNameParameter = command.CreateParameter();
            roleNameParameter.ParameterName = "@RoleName";
            roleNameParameter.Value = roleName;
            command.Parameters.Add(roleNameParameter);

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