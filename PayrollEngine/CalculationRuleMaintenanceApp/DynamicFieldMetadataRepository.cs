using System.Data;
using Microsoft.EntityFrameworkCore;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public sealed class DynamicFieldMetadataRepository(
    PayrollDbContext database)
{
    public async Task<IReadOnlyList<DynamicFieldMetadata>> GetAsync(
        string? search,
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
                "dbo.usp_RuleDataCatalog_GetDynamicFields";
            command.CommandType = CommandType.StoredProcedure;

            var searchParameter = command.CreateParameter();
            searchParameter.ParameterName = "@Search";
            searchParameter.DbType = DbType.String;
            searchParameter.Size = 100;
            searchParameter.Value = string.IsNullOrWhiteSpace(search)
                ? DBNull.Value
                : search.Trim();
            command.Parameters.Add(searchParameter);

            var results = new List<DynamicFieldMetadata>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var masterDataGroupIdOrdinal =
                reader.GetOrdinal("MasterDataGroupId");
            var masterDataGroupCodeOrdinal =
                reader.GetOrdinal("MasterDataGroupCode");
            var masterDataGroupNameOrdinal =
                reader.GetOrdinal("MasterDataGroupName");
            var fieldIdOrdinal =
                reader.GetOrdinal("FieldId");
            var fieldCodeOrdinal =
                reader.GetOrdinal("FieldCode");
            var displayNameOrdinal =
                reader.GetOrdinal("DisplayName");
            var fieldTypeIdOrdinal =
                reader.GetOrdinal("FieldTypeId");
            var fieldTypeCodeOrdinal =
                reader.GetOrdinal("FieldTypeCode");
            var fieldTypeNameOrdinal =
                reader.GetOrdinal("FieldTypeName");
            var isRequiredOrdinal =
                reader.GetOrdinal("IsRequired");
            var isUniqueOrdinal =
                reader.GetOrdinal("IsUnique");
            var displayOrderOrdinal =
                reader.GetOrdinal("DisplayOrder");
            var isActiveOrdinal =
                reader.GetOrdinal("IsActive");

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new DynamicFieldMetadata(
                    reader.GetInt32(masterDataGroupIdOrdinal),
                    reader.GetString(masterDataGroupCodeOrdinal),
                    reader.GetString(masterDataGroupNameOrdinal),
                    reader.GetInt32(fieldIdOrdinal),
                    reader.GetString(fieldCodeOrdinal),
                    reader.GetString(displayNameOrdinal),
                    reader.GetInt32(fieldTypeIdOrdinal),
                    reader.GetString(fieldTypeCodeOrdinal),
                    reader.GetString(fieldTypeNameOrdinal),
                    reader.GetBoolean(isRequiredOrdinal),
                    reader.GetBoolean(isUniqueOrdinal),
                    reader.GetInt32(displayOrderOrdinal),
                    reader.GetBoolean(isActiveOrdinal)));
            }

            return results;
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