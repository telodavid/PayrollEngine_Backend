using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PayrollEngine;

namespace PayrollEngine.CompanyMaintenanceApp;

public sealed class CompanyMaintenanceRepository(PayrollDbContext database)
{
    public Task<IReadOnlyList<CompanyModel>> SearchAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<IReadOnlyList<CompanyModel>>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Company_Search";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@Search", DbType.String, search, 200);

                var companies = new List<CompanyModel>();

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                while (await reader.ReadAsync(token))
                {
                    companies.Add(MapCompany(reader));
                }

                return companies;
            },
            cancellationToken);
    }

    public Task<CompanyModel?> GetByIdAsync(
        int companyId,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync<CompanyModel?>(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Company_GetById";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@CompanyId", DbType.Int32, companyId);

                await using var reader =
                    await command.ExecuteReaderAsync(token);

                if (!await reader.ReadAsync(token))
                {
                    return null;
                }

                return MapCompany(reader);
            },
            cancellationToken);
    }

    public Task<int> CreateAsync(
        SaveCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Company_Create";
                command.CommandType = CommandType.StoredProcedure;

                AddCompanyParameters(command, request);

                var result = await command.ExecuteScalarAsync(token);

                return Convert.ToInt32(result);
            },
            cancellationToken);
    }

    public Task UpdateAsync(
        int companyId,
        SaveCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        return WithConnectionAsync(
            async (connection, token) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "dbo.usp_Company_Update";
                command.CommandType = CommandType.StoredProcedure;

                AddParameter(command, "@CompanyId", DbType.Int32, companyId);
                AddCompanyParameters(command, request);

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

    private static void AddCompanyParameters(
        DbCommand command,
        SaveCompanyRequest request)
    {
        AddParameter(command, "@CompanyName", DbType.String, request.CompanyName, 200);
        AddParameter(command, "@CompanyAddress", DbType.String, request.CompanyAddress, 500);
        AddParameter(command, "@BankAccountNumber", DbType.String, request.BankAccountNumber, 50);
        AddParameter(command, "@TaxId", DbType.String, request.TaxId, 30);
        AddParameter(command, "@PhoneNumber", DbType.String, request.PhoneNumber, 30);
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

    private static CompanyModel MapCompany(DbDataReader reader)
    {
        return new CompanyModel(
            reader.GetInt32(reader.GetOrdinal("CompanyId")),
            reader.GetString(reader.GetOrdinal("CompanyName")),
            ReadNullableString(reader, "CompanyAddress"),
            ReadNullableString(reader, "BankAccountNumber"),
            ReadNullableString(reader, "TaxId"),
            ReadNullableString(reader, "PhoneNumber"));
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
}