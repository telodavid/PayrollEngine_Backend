using Microsoft.EntityFrameworkCore;
using PayrollEngine.AppMaintenanceApp;
using System.Data;
using System.Data.Common;

namespace PayrollEngine.UserMaintenanceApp;

public sealed class AppMaintenanceRepository(PayrollDbContext database)
{
    public async Task<int> CreateAppAsync(
        CreateAppRequest request,
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
            command.CommandText = "dbo.usp_App_Create";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@Code", request.Code);
            AddParameter(command, "@AppName", request.AppName);

            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result is null || result is DBNull)
            {
                throw new InvalidOperationException(
                    "Az alkalmazást létrehozó eljárás nem adott vissza AppId értéket.");
            }

            return Convert.ToInt32(result);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task UpdateAppAsync(
        int appId,
        UpdateAppRequest request,
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
            command.CommandText = "dbo.usp_App_Update";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@AppId", appId);
            AddParameter(command, "@Code", request.Code);
            AddParameter(command, "@AppName", request.AppName);
            AddParameter(command, "@IsActive", request.IsActive);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IReadOnlyList<AppSummary>> GetAppsAsync(
        string? search,
        bool? isActive,
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
            command.CommandText = "dbo.usp_App_Search";
            command.CommandType = CommandType.StoredProcedure;

            AddParameter(command, "@Search", (object?)search ?? DBNull.Value);
            AddParameter(command, "@IsActive", (object?)isActive ?? DBNull.Value);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            var apps = new List<AppSummary>();

            while (await reader.ReadAsync(cancellationToken))
            {
                apps.Add(new AppSummary(
                    reader.GetInt32(reader.GetOrdinal("AppId")),
                    reader.GetString(reader.GetOrdinal("Code")),
                    reader.GetString(reader.GetOrdinal("AppName")),
                    reader.GetBoolean(reader.GetOrdinal("IsActive"))));
            }

            return apps;
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
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}