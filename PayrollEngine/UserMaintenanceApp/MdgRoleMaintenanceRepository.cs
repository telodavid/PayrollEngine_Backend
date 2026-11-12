using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.UserMaintenanceApp;

public sealed class MdgRoleMaintenanceRepository(PayrollDbContext database)
{
    public async Task SetPermissionsAsync(
        int roleId,
        SetMdgRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
        }

        if (request.MasterDataGroupId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MasterDataGroupId));
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
            command.CommandText = "dbo.usp_MDGToRole_SetPermissions";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@RoleId", roleId);
            AddParameter(command, "@MasterDataGroupId", request.MasterDataGroupId);
            AddParameter(command, "@CanRead", request.CanRead);
            AddParameter(command, "@CanEdit", request.CanEdit);

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

    public async Task<IReadOnlyList<MdgRolePermissionSummary>> GetPermissionsAsync(
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
            command.CommandText = "dbo.usp_MDGToRole_GetByRoleId";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@RoleId", roleId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var permissions = new List<MdgRolePermissionSummary>();

            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(new MdgRolePermissionSummary(
                    reader.GetInt32(reader.GetOrdinal("RoleId")),
                    reader.GetInt32(reader.GetOrdinal("MasterDataGroupId")),
                    reader.GetString(reader.GetOrdinal("Code")),
                    reader.GetString(reader.GetOrdinal("Name")),
                    reader.GetBoolean(reader.GetOrdinal("CanRead")),
                    reader.GetBoolean(reader.GetOrdinal("CanEdit"))));
            }

            return permissions;
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