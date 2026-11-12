using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed class MasterDataFieldOptionRepository(PayrollDbContext database)
{
    public Task<int> CreateAsync(
        int fieldId,
        CreateMasterDataFieldOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataFieldOption_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@FieldId", fieldId);
                AddParameter(command, "@OptionCode", request.OptionCode);
                AddParameter(command, "@DisplayName", request.DisplayName);
                AddParameter(command, "@SortOrder", request.SortOrder);

                var result = await command.ExecuteScalarAsync(token);
                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task UpdateAsync(
        int optionId,
        UpdateMasterDataFieldOptionRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataFieldOption_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@OptionId", optionId);
                AddParameter(command, "@OptionCode", request.OptionCode);
                AddParameter(command, "@DisplayName", request.DisplayName);
                AddParameter(command, "@SortOrder", request.SortOrder);
                AddParameter(command, "@IsActive", request.IsActive);

                await command.ExecuteNonQueryAsync(token);
                return true;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<MasterDataFieldOptionModel>> SearchAsync(
        int fieldId,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<MasterDataFieldOptionModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataFieldOption_Search";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@FieldId", fieldId);
                AddParameter(command, "@Search", search);
                AddParameter(command, "@IsActive", isActive);

                var result = new List<MasterDataFieldOptionModel>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(MapOption(reader));
                }

                return result;
            },
            cancellationToken);
    }

    public Task<MasterDataFieldOptionModel?> GetByIdAsync(
        int optionId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<MasterDataFieldOptionModel?>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_MasterDataFieldOption_GetById";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@OptionId", optionId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                return MapOption(reader);
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

    private static MasterDataFieldOptionModel MapOption(DbDataReader reader)
    {
        return new MasterDataFieldOptionModel(
            reader.GetInt32(reader.GetOrdinal("OptionId")),
            reader.GetInt32(reader.GetOrdinal("FieldId")),
            reader.GetString(reader.GetOrdinal("OptionCode")),
            reader.GetString(reader.GetOrdinal("DisplayName")),
            reader.GetInt32(reader.GetOrdinal("SortOrder")),
            reader.GetBoolean(reader.GetOrdinal("IsActive")));
    }
}