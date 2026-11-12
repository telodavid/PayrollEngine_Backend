using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed class MasterDataGroupMaintenanceRepository(
    PayrollDbContext database)
{
    public Task<int> CreateAsync(
        SaveMasterDataGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataGroup_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Code", DbType.String, request.Code, 50);
                AddParameter(command, "@Name", DbType.String, request.Name, 100);

                var result = await command.ExecuteScalarAsync(token);
                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task UpdateAsync(
        int masterDataGroupId,
        SaveMasterDataGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataGroup_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    command,
                    "@MasterDataGroupId",
                    DbType.Int32,
                    masterDataGroupId);

                AddParameter(command, "@Code", DbType.String, request.Code, 50);
                AddParameter(command, "@Name", DbType.String, request.Name, 100);

                await command.ExecuteNonQueryAsync(token);
                return true;
            },
            cancellationToken);
    }

    private async Task<T> WithConnectionAsync<T>(
        Func<DbConnection, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            return await operation(connection, cancellationToken);
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object value,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;

        if (size.HasValue)
            parameter.Size = size.Value;

        command.Parameters.Add(parameter);
    }
}