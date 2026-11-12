using System.Data;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public sealed class DatabaseSchemaRepository(PayrollDbContext database)
{
    public async Task<DatabaseSchemaMetadata> GetSchemaAsync(
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
            command.CommandText = "dbo.usp_RuleDataCatalog_GetSchema";
            command.CommandType = CommandType.StoredProcedure;

            var columns = new List<DatabaseColumnMetadata>();
            var relationships = new List<DatabaseRelationshipMetadata>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new DatabaseColumnMetadata(
                    reader.GetString(reader.GetOrdinal("SchemaName")),
                    reader.GetString(reader.GetOrdinal("TableName")),
                    Convert.ToInt32(reader["ColumnOrder"]),
                    reader.GetString(reader.GetOrdinal("ColumnName")),
                    reader.GetString(reader.GetOrdinal("DataType")),
                    Convert.ToInt32(reader["MaxLength"]),
                    Convert.ToByte(reader["NumericPrecision"]),
                    Convert.ToByte(reader["NumericScale"]),
                    reader.GetBoolean(reader.GetOrdinal("IsNullable")),
                    reader.GetBoolean(reader.GetOrdinal("IsIdentity")),
                    reader.GetBoolean(reader.GetOrdinal("IsPrimaryKey")),
                    reader.GetBoolean(reader.GetOrdinal("IsForeignKey"))));
            }

            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    relationships.Add(new DatabaseRelationshipMetadata(
                        reader.GetString(reader.GetOrdinal("ForeignKeyName")),
                        reader.GetString(reader.GetOrdinal("SourceSchema")),
                        reader.GetString(reader.GetOrdinal("SourceTable")),
                        reader.GetString(reader.GetOrdinal("SourceColumn")),
                        reader.GetString(reader.GetOrdinal("TargetSchema")),
                        reader.GetString(reader.GetOrdinal("TargetTable")),
                        reader.GetString(reader.GetOrdinal("TargetColumn"))));
                }
            }

            return new DatabaseSchemaMetadata(columns, relationships);
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