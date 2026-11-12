using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.UserMaintenanceApp;

public sealed class AppToRoleMaintenanceRepository(PayrollDbContext database)
{
    public async Task AssignAppToRoleAsync(
        int roleId,
        int appId,
        CancellationToken cancellationToken = default)
    {
        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
        }

        if (appId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appId));
        }

        await ExecuteNonQueryAsync(
            "dbo.usp_AppToRole_Assign",
            roleId,
            appId,
            cancellationToken);
    }

    public async Task RemoveAppFromRoleAsync(
        int roleId,
        int appId,
        CancellationToken cancellationToken = default)
    {
        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
        }

        if (appId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appId));
        }

        await ExecuteNonQueryAsync(
            "dbo.usp_AppToRole_Remove",
            roleId,
            appId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AppSummary>> GetAppsForRoleAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
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
            command.CommandText = "dbo.usp_AppToRole_GetByRoleId";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@RoleId", roleId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var apps = new List<AppSummary>();

            while (await reader.ReadAsync(cancellationToken))
            {
                apps.Add(new AppSummary(
                    reader.GetInt32(reader.GetOrdinal("AppId")),
                    reader.GetString(reader.GetOrdinal("Code")),
                    reader.GetString(reader.GetOrdinal("AppName")),
                    reader.GetBoolean(reader.GetOrdinal("IsActive"))));
            }

            return apps;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task ExecuteNonQueryAsync(
        string procedureName,
        int roleId,
        int appId,
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

            AddParameter(command, "@RoleId", roleId);
            AddParameter(command, "@AppId", appId);

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
}