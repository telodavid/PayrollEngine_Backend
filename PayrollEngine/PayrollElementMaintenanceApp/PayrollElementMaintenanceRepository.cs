using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed class PayrollElementMaintenanceRepository(PayrollDbContext database)
{
    public Task<IReadOnlyList<PayrollElementModel>> SearchAsync(
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<PayrollElementModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_PayrollElement_Search";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Search", DbType.String, search, 100);
                AddParameter(command, "@IsActive", DbType.Boolean, isActive);

                var result = new List<PayrollElementModel>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    result.Add(MapPayrollElement(reader));
                }

                return result;
            },
            cancellationToken);
    }

    public Task<PayrollElementModel?> GetByIdAsync(
        int payrollElementId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<PayrollElementModel?>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_PayrollElement_GetById";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    command,
                    "@PayrollElementId",
                    DbType.Int32,
                    payrollElementId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                return await reader.ReadAsync(token)
                    ? MapPayrollElement(reader)
                    : null;
            },
            cancellationToken);
    }

    public Task<int> CreateAsync(
        CreatePayrollElementRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_PayrollElement_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Code", DbType.String, request.Code, 50);
                AddParameter(command, "@Name", DbType.String, request.Name, 100);

                var result = await command.ExecuteScalarAsync(token);

                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task<PayrollElementModel> UpdateAsync(
        int payrollElementId,
        UpdatePayrollElementRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_PayrollElement_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    command,
                    "@PayrollElementId",
                    DbType.Int32,
                    payrollElementId);
                AddParameter(command, "@Code", DbType.String, request.Code, 50);
                AddParameter(command, "@Name", DbType.String, request.Name, 100);
                AddParameter(
                    command,
                    "@IsActive",
                    DbType.Boolean,
                    request.IsActive);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException(
                        "A bérelem módosítása nem adott vissza eredményt.");
                }

                return MapPayrollElement(reader);
            },
            cancellationToken);
    }
    public Task<IReadOnlyList<PayrollElementFieldValueHistoryModel>> GetFieldValueHistoryAsync(
    int payrollElementId,
    int fieldId,
    int userId,
    CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<PayrollElementFieldValueHistoryModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "dbo.usp_PayrollElementFieldValue_GetHistory";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    command,
                    "@PayrollElementId",
                    DbType.Int32,
                    payrollElementId);

                AddParameter(
                    command,
                    "@FieldId",
                    DbType.Int32,
                    fieldId);

                AddParameter(
                    command,
                    "@UserId",
                    DbType.Int32,
                    userId);

                var result = new List<PayrollElementFieldValueHistoryModel>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    var validToOrdinal = reader.GetOrdinal("ValidTo");
                    var optionNameOrdinal =
                        reader.GetOrdinal("OptionDisplayName");

                    result.Add(new PayrollElementFieldValueHistoryModel(
                        ReadNullableString(reader, "FieldValue"),
                        ReadNullableInt32(reader, "OptionId"),
                        reader.IsDBNull(optionNameOrdinal)
                            ? null
                            : reader.GetString(optionNameOrdinal),
                        DateOnly.FromDateTime(
                            reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                        reader.IsDBNull(validToOrdinal)
                            ? null
                            : DateOnly.FromDateTime(
                                reader.GetDateTime(validToOrdinal))));
                }

                return result;
            },
            cancellationToken);
    }
    public Task<IReadOnlyList<PayrollElementFieldGroupModel>> GetFieldValuesAsync(
        int payrollElementId,
        int userId,
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<PayrollElementFieldGroupModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "dbo.usp_PayrollElementFieldValue_GetByPayrollElementId";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    command,
                    "@PayrollElementId",
                    DbType.Int32,
                    payrollElementId);
                AddParameter(command, "@UserId", DbType.Int32, userId);
                AddDateParameter(command, "@AsOfDate", asOfDate);

                var fields = new List<FieldRow>();
                var optionsByFieldId =
                    new Dictionary<int, List<PayrollElementFieldOptionModel>>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    fields.Add(new FieldRow(
                        reader.GetInt32(reader.GetOrdinal("MasterDataGroupId")),
                        reader.GetString(reader.GetOrdinal("MasterDataGroupCode")),
                        reader.GetString(reader.GetOrdinal("MasterDataGroupName")),
                        reader.GetInt32(reader.GetOrdinal("FieldId")),
                        reader.GetString(reader.GetOrdinal("Code")),
                        reader.GetString(reader.GetOrdinal("DisplayName")),
                        reader.GetInt32(reader.GetOrdinal("FieldTypeId")),
                        reader.GetString(reader.GetOrdinal("FieldTypeCode")),
                        reader.GetString(reader.GetOrdinal("FieldTypeName")),
                        reader.GetBoolean(reader.GetOrdinal("IsRequired")),
                        reader.GetBoolean(reader.GetOrdinal("IsUnique")),
                        reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
                        reader.GetBoolean(reader.GetOrdinal("CanRead")),
                        reader.GetBoolean(reader.GetOrdinal("CanEdit")),
                        ReadNullableString(reader, "FieldValue"),
                        ReadNullableInt32(reader, "OptionId"),
                        ReadNullableDate(reader, "ValidFrom"),
                        ReadNullableDate(reader, "ValidTo")));
                }

                if (await reader.NextResultAsync(token))
                {
                    while (await reader.ReadAsync(token))
                    {
                        var option = new PayrollElementFieldOptionModel(
                            reader.GetInt32(reader.GetOrdinal("FieldId")),
                            reader.GetInt32(reader.GetOrdinal("OptionId")),
                            reader.GetString(reader.GetOrdinal("OptionCode")),
                            reader.GetString(reader.GetOrdinal("DisplayName")),
                            reader.GetInt32(reader.GetOrdinal("SortOrder")));

                        if (!optionsByFieldId.TryGetValue(
                                option.FieldId,
                                out var fieldOptions))
                        {
                            fieldOptions = [];
                            optionsByFieldId.Add(option.FieldId, fieldOptions);
                        }

                        fieldOptions.Add(option);
                    }
                }

                return fields
                    .GroupBy(field => field.MasterDataGroupId)
                    .Select(group =>
                    {
                        var first = group.First();

                        var groupFields = group
                            .OrderBy(field => field.DisplayOrder)
                            .ThenBy(field => field.DisplayName)
                            .Select(field => new PayrollElementFieldModel(
                                field.FieldId,
                                field.Code,
                                field.DisplayName,
                                field.FieldTypeId,
                                field.FieldTypeCode,
                                field.FieldTypeName,
                                field.IsRequired,
                                field.IsUnique,
                                field.DisplayOrder,
                                field.CanRead,
                                field.CanEdit,
                                field.FieldValue,
                                field.OptionId,
                                field.ValidFrom,
                                field.ValidTo,
                                optionsByFieldId.TryGetValue(
                                    field.FieldId,
                                    out var fieldOptions)
                                        ? fieldOptions
                                        : []))
                            .ToArray();

                        return new PayrollElementFieldGroupModel(
                            first.MasterDataGroupId,
                            first.MasterDataGroupCode,
                            first.MasterDataGroupName,
                            groupFields);
                    })
                    .OrderBy(group => group.Name)
                    .ToArray();
            },
            cancellationToken);
    }

    public Task<PayrollElementFieldValueModel> SaveFieldValueAsync(
        int payrollElementId,
        int userId,
        SavePayrollElementFieldValueRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_PayrollElementFieldValue_Save";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    command,
                    "@PayrollElementId",
                    DbType.Int32,
                    payrollElementId);
                AddParameter(command, "@FieldId", DbType.Int32, request.FieldId);
                AddParameter(
                    command,
                    "@FieldValue",
                    DbType.String,
                    request.FieldValue,
                    4000);
                AddParameter(command, "@OptionId", DbType.Int32, request.OptionId);
                AddDateParameter(command, "@ValidFrom", request.ValidFrom);
                AddDateParameter(command, "@ValidTo", request.ValidTo);
                AddParameter(command, "@UserId", DbType.Int32, userId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException(
                        "A mezőérték mentése nem adott vissza eredményt.");
                }

                return MapFieldValue(reader);
            },
            cancellationToken);
    }

    public Task<PayrollElementFieldValueModel> EndFieldValueAsync(
        int payrollElementId,
        int fieldId,
        int userId,
        EndPayrollElementFieldValueRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_PayrollElementFieldValue_End";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(
                    command,
                    "@PayrollElementId",
                    DbType.Int32,
                    payrollElementId);
                AddParameter(command, "@FieldId", DbType.Int32, fieldId);
                AddDateParameter(command, "@ValidTo", request.ValidTo);
                AddParameter(command, "@UserId", DbType.Int32, userId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    throw new InvalidOperationException(
                        "A mezőérték lezárása nem adott vissza eredményt.");
                }

                return MapFieldValue(reader);
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
        DbType type,
        object? value,
        int? size = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;

        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }

        command.Parameters.Add(parameter);
    }

    private static void AddDateParameter(
        DbCommand command,
        string name,
        DateOnly? value)
    {
        AddParameter(
            command,
            name,
            DbType.Date,
            value.HasValue
                ? value.Value.ToDateTime(TimeOnly.MinValue)
                : null);
    }

    private static PayrollElementModel MapPayrollElement(DbDataReader reader)
    {
        return new PayrollElementModel(
            reader.GetInt32(reader.GetOrdinal("PayrollElementId")),
            reader.GetString(reader.GetOrdinal("Code")),
            reader.GetString(reader.GetOrdinal("Name")),
            reader.GetBoolean(reader.GetOrdinal("IsActive")));
    }

    private static PayrollElementFieldValueModel MapFieldValue(
        DbDataReader reader)
    {
        return new PayrollElementFieldValueModel(
            reader.GetInt32(reader.GetOrdinal("PayrollElementId")),
            reader.GetInt32(reader.GetOrdinal("FieldId")),
            ReadNullableString(reader, "FieldValue"),
            ReadNullableInt32(reader, "OptionId"),
            ReadDate(reader, "ValidFrom"),
            ReadNullableDate(reader, "ValidTo"));
    }

    private static string? ReadNullableString(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt32(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetInt32(ordinal);
    }

    private static DateOnly ReadDate(
        DbDataReader reader,
        string columnName)
    {
        return DateOnly.FromDateTime(
            reader.GetDateTime(reader.GetOrdinal(columnName)));
    }

    private static DateOnly? ReadNullableDate(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : DateOnly.FromDateTime(reader.GetDateTime(ordinal));
    }

    private sealed record FieldRow(
        int MasterDataGroupId,
        string MasterDataGroupCode,
        string MasterDataGroupName,
        int FieldId,
        string Code,
        string DisplayName,
        int FieldTypeId,
        string FieldTypeCode,
        string FieldTypeName,
        bool IsRequired,
        bool IsUnique,
        int DisplayOrder,
        bool CanRead,
        bool CanEdit,
        string? FieldValue,
        int? OptionId,
        DateOnly? ValidFrom,
        DateOnly? ValidTo);
}