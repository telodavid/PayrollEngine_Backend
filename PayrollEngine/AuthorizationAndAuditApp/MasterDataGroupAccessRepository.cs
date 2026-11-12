using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class MasterDataGroupAccessRepository(PayrollDbContext database)
{
    public async Task<IReadOnlyList<MasterDataGroupAccess>> GetByUserIdAsync(
        int userId,
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
            command.CommandText = "dbo.usp_MasterDataGroup_GetAccessByUserId";
            command.CommandType = CommandType.StoredProcedure;

            var userIdParameter = command.CreateParameter();
            userIdParameter.ParameterName = "@UserId";
            userIdParameter.Value = userId;
            command.Parameters.Add(userIdParameter);

            var rows = new List<MasterDataGroupRoleAccessRow>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new MasterDataGroupRoleAccessRow(
                    reader.GetInt32(reader.GetOrdinal("MasterDataGroupId")),
                    reader.GetString(reader.GetOrdinal("MasterDataGroupCode")),
                    reader.GetString(reader.GetOrdinal("MasterDataGroupName")),
                    reader.GetInt32(reader.GetOrdinal("RoleId")),
                    reader.GetString(reader.GetOrdinal("RoleCode")),
                    reader.GetString(reader.GetOrdinal("RoleName")),
                    reader.GetBoolean(reader.GetOrdinal("CanRead")),
                    reader.GetBoolean(reader.GetOrdinal("CanEdit"))));
            }

            return rows
                .GroupBy(row => row.MasterDataGroupId)
                .Select(group =>
                {
                    var first = group.First();
                    var roles = group
                        .Select(row => new MasterDataGroupRoleAccess(
                            row.RoleId,
                            row.RoleCode,
                            row.RoleName,
                            row.CanRead,
                            row.CanEdit))
                        .ToArray();

                    return new MasterDataGroupAccess(
                        first.MasterDataGroupId,
                        first.Code,
                        first.Name,
                        roles.Any(role => role.CanRead),
                        roles.Any(role => role.CanEdit),
                        roles);
                })
                .OrderBy(group => group.Name)
                .ToArray();
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private sealed record MasterDataGroupRoleAccessRow(
        int MasterDataGroupId,
        string Code,
        string Name,
        int RoleId,
        string RoleCode,
        string RoleName,
        bool CanRead,
        bool CanEdit);
}