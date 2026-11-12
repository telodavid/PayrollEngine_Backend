using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class EmployeeFieldValueRepository(PayrollDbContext database)
{
    public async Task<IReadOnlyList<EmployeeFieldGroupModel>> GetByEmployeeIdAsync(
        int employeeId,
        int userId,
        DateOnly? asOfDate = null,
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
            command.CommandText =
                "dbo.usp_EmployeeFieldValue_GetByEmployeeId";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);
            AddParameter(command, "@UserId", userId);
            AddParameter(
                command,
                "@AsOfDate",
                asOfDate?.ToDateTime(TimeOnly.MinValue),
                DbType.Date);

            var fields = new List<FieldRow>();
            var optionsByFieldId =
                new Dictionary<int, List<EmployeeFieldOptionModel>>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
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

            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var option = new EmployeeFieldOptionModel(
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
                        .Select(field => new EmployeeFieldModel(
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

                    return new EmployeeFieldGroupModel(
                        first.MasterDataGroupId,
                        first.MasterDataGroupCode,
                        first.MasterDataGroupName,
                        groupFields);
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

    public async Task<EmployeeFieldValueSaveResult> SaveAsync(
        int employeeId,
        int fieldId,
        int userId,
        SaveEmployeeFieldValueRequest request,
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
            command.CommandText = "dbo.usp_EmployeeFieldValue_Save";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);
            AddParameter(command, "@FieldId", fieldId);
            AddParameter(command, "@UserId", userId);
            AddParameter(
                command,
                "@ValidFrom",
                request.ValidFrom.ToDateTime(TimeOnly.MinValue),
                DbType.Date);
            AddParameter(command, "@FieldValue", request.FieldValue);
            AddParameter(command, "@OptionId", request.OptionId);
            AddParameter(
                command,
                "@RetroPayrollRunFrom",
                request.RetroPayrollRunFrom?.ToDateTime(TimeOnly.MinValue),
                DbType.Date);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A mezőérték mentése nem adott vissza eredményt.");
            }

            return new EmployeeFieldValueSaveResult(
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                reader.GetInt32(reader.GetOrdinal("FieldId")),
                DateOnly.FromDateTime(
                    reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                ReadNullableDate(reader, "ValidTo"),
                reader.GetBoolean(
                    reader.GetOrdinal("RequiresRetroPayroll")),
                ReadNullableDate(reader, "SuggestedPayrollRunFrom"));
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<EmployeeFieldValueEndResult> EndAsync(
        int employeeId,
        int fieldId,
        int userId,
        EndEmployeeFieldValueRequest request,
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
            command.CommandText = "dbo.usp_EmployeeFieldValue_End";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@EmployeeId", employeeId);
            AddParameter(command, "@FieldId", fieldId);
            AddParameter(command, "@UserId", userId);
            AddParameter(
                command,
                "@ValidFrom",
                request.ValidFrom.ToDateTime(TimeOnly.MinValue),
                DbType.Date);
            AddParameter(
                command,
                "@ValidTo",
                request.ValidTo.ToDateTime(TimeOnly.MinValue),
                DbType.Date);
            AddParameter(
                command,
                "@RetroPayrollRunFrom",
                request.RetroPayrollRunFrom?.ToDateTime(TimeOnly.MinValue),
                DbType.Date);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "A mezőérték lezárása nem adott vissza eredményt.");
            }

            var validTo = ReadNullableDate(reader, "ValidTo")
                ?? throw new InvalidOperationException(
                    "A lezárt mezőérték érvényességi vége hiányzik.");

            return new EmployeeFieldValueEndResult(
                reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                reader.GetInt32(reader.GetOrdinal("FieldId")),
                DateOnly.FromDateTime(
                    reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                validTo,
                reader.GetBoolean(
                    reader.GetOrdinal("RequiresRetroPayroll")),
                ReadNullableDate(reader, "SuggestedPayrollRunFrom"));
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
        string parameterName,
        object? value,
        DbType? dbType = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;

        if (dbType.HasValue)
        {
            parameter.DbType = dbType.Value;
        }

        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
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