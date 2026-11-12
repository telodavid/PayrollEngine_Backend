using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed class MasterDataFieldRepository(PayrollDbContext database)
{
    public Task<int> CreateAsync(
        CreateMasterDataFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataField_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@MasterDataGroupId", request.MasterDataGroupId);
                AddParameter(command, "@Code", request.Code);
                AddParameter(command, "@DisplayName", request.DisplayName);
                AddParameter(command, "@FieldTypeId", request.FieldTypeId);
                AddParameter(command, "@IsRequired", request.IsRequired);
                AddParameter(command, "@IsUnique", request.IsUnique);
                AddParameter(command, "@DisplayOrder", request.DisplayOrder);

                var result = await command.ExecuteScalarAsync(token);
                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task UpdateAsync(
        int fieldId,
        UpdateMasterDataFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataField_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@FieldId", fieldId);
                AddParameter(command, "@MasterDataGroupId", request.MasterDataGroupId);
                AddParameter(command, "@Code", request.Code);
                AddParameter(command, "@DisplayName", request.DisplayName);
                AddParameter(command, "@FieldTypeId", request.FieldTypeId);
                AddParameter(command, "@IsRequired", request.IsRequired);
                AddParameter(command, "@IsUnique", request.IsUnique);
                AddParameter(command, "@DisplayOrder", request.DisplayOrder);
                AddParameter(command, "@IsActive", request.IsActive);

                await command.ExecuteNonQueryAsync(token);
                return true;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<MasterDataFieldModel>> SearchAsync(
        int? masterDataGroupId,
        int? fieldTypeId,
        bool? isActive,
        string? search,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<MasterDataFieldModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataField_Search";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@MasterDataGroupId", masterDataGroupId);
                AddParameter(command, "@FieldTypeId", fieldTypeId);
                AddParameter(command, "@IsActive", isActive);
                AddParameter(command, "@Search", search);

                var result = new List<MasterDataFieldModel>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(MapField(reader));
                }

                return result;
            },
            cancellationToken);
    }

    public Task<MasterDataFieldModel?> GetByIdAsync(
        int fieldId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<MasterDataFieldModel?>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataField_GetById";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@FieldId", fieldId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                return MapField(reader);
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
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await operation(connection, cancellationToken);
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
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static MasterDataFieldModel MapField(DbDataReader reader)
    {
        return new MasterDataFieldModel(
            reader.GetInt32(reader.GetOrdinal("FieldId")),
            reader.GetInt32(reader.GetOrdinal("MasterDataGroupId")),
            reader.GetString(reader.GetOrdinal("MasterDataGroupCode")),
            reader.GetString(reader.GetOrdinal("MasterDataGroupName")),
            reader.GetString(reader.GetOrdinal("Code")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            reader.GetInt32(reader.GetOrdinal("FieldTypeId")),
            reader.GetString(reader.GetOrdinal("FieldTypeCode")),
            reader.GetString(reader.GetOrdinal("FieldTypeName")),
            reader.GetBoolean(reader.GetOrdinal("IsRequired")),
            reader.GetBoolean(reader.GetOrdinal("IsUnique")),
            reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
            reader.GetBoolean(reader.GetOrdinal("IsActive")));
    }
}