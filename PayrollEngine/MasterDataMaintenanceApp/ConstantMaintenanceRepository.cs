using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class ConstantMaintenanceRepository(PayrollDbContext database)
{
    public Task<IReadOnlyList<ConstantItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => WithConnectionAsync<IReadOnlyList<ConstantItem>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Constant_GetAll";
                command.CommandType = CommandType.StoredProcedure;

                var result = new List<ConstantItem>();
                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(MapConstant(reader));
                }

                return result;
            },
            cancellationToken);

    public Task<ConstantItem> CreateAsync(
        CreateConstantRequest request,
        CancellationToken cancellationToken = default)
        => WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Constant_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@ConstantId", DbType.AnsiString, request.ConstantId);
                AddParameter(command, "@ConstantName", DbType.String, request.ConstantName);
                AddDecimalParameter(command, "@Value", request.Value);
                AddParameter(
                    command,
                    "@ValidFrom",
                    DbType.Date,
                    request.ValidFrom.ToDateTime(TimeOnly.MinValue));
                AddParameter(
                    command,
                    "@ValidTo",
                    DbType.Date,
                    ToDateTime(request.ValidTo));

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException(
                        "A konstans létrehozása nem adott vissza eredményt.");
                }

                return MapConstant(reader);
            },
            cancellationToken);

    public Task<ConstantItem> UpdateAsync(
        string constantId,
        DateOnly validFrom,
        UpdateConstantRequest request,
        CancellationToken cancellationToken = default)
        => WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Constant_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@ConstantId", DbType.AnsiString, constantId);
                AddParameter(
                    command,
                    "@ValidFrom",
                    DbType.Date,
                    validFrom.ToDateTime(TimeOnly.MinValue));
                AddParameter(command, "@ConstantName", DbType.String, request.ConstantName);
                AddDecimalParameter(command, "@Value", request.Value);
                AddParameter(
                    command,
                    "@ValidTo",
                    DbType.Date,
                    ToDateTime(request.ValidTo));

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException(
                        "A konstans módosítása nem adott vissza eredményt.");
                }

                return MapConstant(reader);
            },
            cancellationToken);

    public Task DeleteAsync(
        string constantId,
        DateOnly validFrom,
        CancellationToken cancellationToken = default)
        => WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Constant_Delete";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@ConstantId", DbType.AnsiString, constantId);
                AddParameter(
                    command,
                    "@ValidFrom",
                    DbType.Date,
                    validFrom.ToDateTime(TimeOnly.MinValue));

                await command.ExecuteNonQueryAsync(token);
                return true;
            },
            cancellationToken);

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

    private static ConstantItem MapConstant(DbDataReader reader)
    {
        var validToOrdinal = reader.GetOrdinal("ValidTo");

        return new ConstantItem(
            reader.GetString(reader.GetOrdinal("ConstantId")),
            reader.GetString(reader.GetOrdinal("ConstantName")),
            reader.GetDecimal(reader.GetOrdinal("Value")),
            DateOnly.FromDateTime(
                reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
            reader.IsDBNull(validToOrdinal)
                ? null
                : DateOnly.FromDateTime(reader.GetDateTime(validToOrdinal)));
    }

    private static DateTime? ToDateTime(DateOnly? value)
        => value?.ToDateTime(TimeOnly.MinValue);

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddDecimalParameter(
        DbCommand command,
        string name,
        decimal value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Decimal;
        parameter.Precision = 18;
        parameter.Scale = 6;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}