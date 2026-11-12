using System.Data;
using Microsoft.EntityFrameworkCore;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportDatabaseMetadataRepository(
    PayrollDbContext database)
{
    public async Task<ReportDatabaseMetadata> LoadAsync(
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
                "dbo.usp_ReportBuilder_GetDatabaseMetadata";

            command.CommandType = CommandType.StoredProcedure;

            var metadata = new ReportDatabaseMetadata();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var schemaName = reader.GetOrdinal("SchemaName");
            var tableName = reader.GetOrdinal("TableName");
            var tableDisplayName = reader.GetOrdinal("TableDisplayName");
            var columnOrder = reader.GetOrdinal("ColumnOrder");
            var columnName = reader.GetOrdinal("ColumnName");
            var sqlType = reader.GetOrdinal("SqlType");
            var dataType = reader.GetOrdinal("DataType");
            var isNullable = reader.GetOrdinal("IsNullable");
            var isIdentity = reader.GetOrdinal("IsIdentity");
            var isPrimaryKey = reader.GetOrdinal("IsPrimaryKey");
            var primaryKeyOrder = reader.GetOrdinal("PrimaryKeyOrder");
            var maxLengthBytes = reader.GetOrdinal("MaxLengthBytes");
            var numericPrecision = reader.GetOrdinal("NumericPrecision");
            var numericScale = reader.GetOrdinal("NumericScale");

            while (await reader.ReadAsync(cancellationToken))
            {
                metadata.Columns.Add(new ReportDatabaseColumn
                {
                    SchemaName = reader.GetString(schemaName),
                    TableName = reader.GetString(tableName),
                    TableDisplayName = reader.GetString(tableDisplayName),
                    ColumnOrder = reader.GetInt32(columnOrder),
                    ColumnName = reader.GetString(columnName),
                    SqlType = reader.GetString(sqlType),
                    DataType = reader.GetString(dataType),
                    IsNullable = reader.GetBoolean(isNullable),
                    IsIdentity = reader.GetBoolean(isIdentity),
                    IsPrimaryKey = reader.GetBoolean(isPrimaryKey),
                    PrimaryKeyOrder = reader.IsDBNull(primaryKeyOrder)
                        ? null
                        : Convert.ToInt32(reader.GetValue(primaryKeyOrder)),
                    MaxLengthBytes = reader.GetInt16(maxLengthBytes),
                    NumericPrecision = reader.GetByte(numericPrecision),
                    NumericScale = reader.GetByte(numericScale)
                });
            }

            if (!await reader.NextResultAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Az adatbázis-metaadat lekérdezése nem adta vissza " +
                    "az idegenkulcs-kapcsolatok eredménytábláját.");
            }

            var foreignKeyId = reader.GetOrdinal("ForeignKeyId");
            var foreignKeyName = reader.GetOrdinal("ForeignKeyName");
            var foreignKeyColumnOrder = reader.GetOrdinal("ColumnOrder");
            var parentSchemaName = reader.GetOrdinal("ParentSchemaName");
            var parentTableName = reader.GetOrdinal("ParentTableName");
            var parentColumnName = reader.GetOrdinal("ParentColumnName");

            var referencedSchemaName =
                reader.GetOrdinal("ReferencedSchemaName");

            var referencedTableName =
                reader.GetOrdinal("ReferencedTableName");

            var referencedColumnName =
                reader.GetOrdinal("ReferencedColumnName");

            var foreignKeyIsNullable = reader.GetOrdinal("IsNullable");
            var isDisabled = reader.GetOrdinal("IsDisabled");
            var isNotTrusted = reader.GetOrdinal("IsNotTrusted");

            while (await reader.ReadAsync(cancellationToken))
            {
                metadata.ForeignKeyColumns.Add(
                    new ReportDatabaseForeignKeyColumn
                    {
                        ForeignKeyId = reader.GetInt32(foreignKeyId),
                        ForeignKeyName = reader.GetString(foreignKeyName),
                        ColumnOrder = reader.GetInt32(foreignKeyColumnOrder),
                        ParentSchemaName = reader.GetString(parentSchemaName),
                        ParentTableName = reader.GetString(parentTableName),
                        ParentColumnName = reader.GetString(parentColumnName),
                        ReferencedSchemaName =
                            reader.GetString(referencedSchemaName),
                        ReferencedTableName =
                            reader.GetString(referencedTableName),
                        ReferencedColumnName =
                            reader.GetString(referencedColumnName),
                        IsNullable =
                            reader.GetBoolean(foreignKeyIsNullable),
                        IsDisabled = reader.GetBoolean(isDisabled),
                        IsNotTrusted = reader.GetBoolean(isNotTrusted)
                    });
            }

            return metadata;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IReadOnlyList<ReportDynamicFieldMetadata>>
        LoadDynamicFieldsAsync(
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
                "dbo.usp_ReportBuilder_GetDynamicFieldMetadata";

            command.CommandType = CommandType.StoredProcedure;

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var masterDataFieldId = reader.GetOrdinal("MasterDataFieldId");
            var fieldCode = reader.GetOrdinal("FieldCode");
            var displayName = reader.GetOrdinal("DisplayName");
            var displayOrder = reader.GetOrdinal("DisplayOrder");
            var isRequired = reader.GetOrdinal("IsRequired");
            var isUnique = reader.GetOrdinal("IsUnique");
            var masterDataGroupId = reader.GetOrdinal("MasterDataGroupId");
            var groupCode = reader.GetOrdinal("GroupCode");
            var groupName = reader.GetOrdinal("GroupName");
            var fieldTypeCode = reader.GetOrdinal("FieldTypeCode");
            var fieldTypeName = reader.GetOrdinal("FieldTypeName");
            var ownerType = reader.GetOrdinal("OwnerType");
            var ownerSchema = reader.GetOrdinal("OwnerSchema");
            var ownerEntity = reader.GetOrdinal("OwnerEntity");
            var ownerIdColumn = reader.GetOrdinal("OwnerIdColumn");
            var valueSchema = reader.GetOrdinal("ValueSchema");
            var valueEntity = reader.GetOrdinal("ValueEntity");

            var ownerForeignKeyColumn =
                reader.GetOrdinal("OwnerForeignKeyColumn");

            var fieldIdColumn = reader.GetOrdinal("FieldIdColumn");
            var valueColumn = reader.GetOrdinal("ValueColumn");
            var optionIdColumn = reader.GetOrdinal("OptionIdColumn");
            var validFromColumn = reader.GetOrdinal("ValidFromColumn");
            var validToColumn = reader.GetOrdinal("ValidToColumn");

            var fields = new List<ReportDynamicFieldMetadata>();

            while (await reader.ReadAsync(cancellationToken))
            {
                fields.Add(new ReportDynamicFieldMetadata
                {
                    MasterDataFieldId = reader.GetInt32(masterDataFieldId),
                    FieldCode = reader.GetString(fieldCode),
                    DisplayName = reader.GetString(displayName),
                    DisplayOrder = reader.GetInt32(displayOrder),
                    IsRequired = reader.GetBoolean(isRequired),
                    IsUnique = reader.GetBoolean(isUnique),
                    MasterDataGroupId = reader.GetInt32(masterDataGroupId),
                    GroupCode = reader.GetString(groupCode),
                    GroupName = reader.GetString(groupName),
                    FieldTypeCode = reader.GetString(fieldTypeCode),
                    FieldTypeName = reader.GetString(fieldTypeName),
                    OwnerType = reader.GetString(ownerType),
                    OwnerSchema = reader.GetString(ownerSchema),
                    OwnerEntity = reader.GetString(ownerEntity),
                    OwnerIdColumn = reader.GetString(ownerIdColumn),
                    ValueSchema = reader.GetString(valueSchema),
                    ValueEntity = reader.GetString(valueEntity),
                    OwnerForeignKeyColumn =
                        reader.GetString(ownerForeignKeyColumn),
                    FieldIdColumn = reader.GetString(fieldIdColumn),
                    ValueColumn = reader.GetString(valueColumn),
                    OptionIdColumn = reader.GetString(optionIdColumn),
                    ValidFromColumn = reader.IsDBNull(validFromColumn)
                        ? null
                        : reader.GetString(validFromColumn),
                    ValidToColumn = reader.IsDBNull(validToColumn)
                        ? null
                        : reader.GetString(validToColumn)
                });
            }

            return fields;
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