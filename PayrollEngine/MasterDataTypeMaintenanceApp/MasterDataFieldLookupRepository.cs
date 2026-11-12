using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed class MasterDataFieldLookupRepository(
    PayrollDbContext database)
{
    public Task<IReadOnlyList<FieldTypeModel>> GetFieldTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteQueryAsync(
            "dbo.usp_FieldType_GetAll",
            reader => new FieldTypeModel(
                reader.GetInt32(reader.GetOrdinal("FieldTypeId")),
                reader.GetString(reader.GetOrdinal("Code")),
                reader.GetString(reader.GetOrdinal("Name"))),
            cancellationToken);
    }

    public Task<IReadOnlyList<MasterDataGroupModel>> GetMasterDataGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteQueryAsync(
            "dbo.usp_MasterDataGroup_GetAll",
            reader => new MasterDataGroupModel(
                reader.GetInt32(reader.GetOrdinal("MasterDataGroupId")),
                reader.GetString(reader.GetOrdinal("Code")),
                reader.GetString(reader.GetOrdinal("Name"))),
            cancellationToken);
    }

    private async Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(
        string procedureName,
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

            var result = new List<T>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(map(reader));
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