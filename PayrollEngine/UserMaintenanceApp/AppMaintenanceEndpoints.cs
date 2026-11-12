using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AppMaintenanceApp;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.UserMaintenanceApp;

public static class AppMaintenanceEndpoints
{
    public static WebApplication MapAppMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/apps", GetAppsAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("GetApps");

        app.MapPost("/api/apps", CreateAppAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("CreateApp");

        app.MapPut("/api/apps/{appId:int}", UpdateAppAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("UpdateApp");

        return app;
    }

    private static async Task<IResult> GetAppsAsync(
        string? search,
        bool? isActive,
        AppMaintenanceRepository apps,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await apps.GetAppsAsync(
                search,
                isActive,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("AppMaintenanceEndpoints");
            logger.LogError(
                exception,
                "Hiba történt az alkalmazások lekérdezésekor.");

            return Results.Problem(
                title: "Az alkalmazások lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateAppAsync(
        CreateAppRequest request,
        AppMaintenanceRepository apps,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AppMaintenanceEndpoints");
        var updatedValue =
            $"Code={request.Code}; AppName={request.AppName}; IsActive=true";

        if (string.IsNullOrWhiteSpace(request.Code) ||
            request.Code.Length > 50)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", null, null, updatedValue, false,
                "Az alkalmazáskód érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Az alkalmazáskód nem lehet üres, és legfeljebb 50 karakter lehet."
            });
        }

        if (string.IsNullOrWhiteSpace(request.AppName) ||
            request.AppName.Length > 100)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", null, null, updatedValue, false,
                "Az alkalmazás neve érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Az alkalmazás neve nem lehet üres, és legfeljebb 100 karakter lehet."
            });
        }

        try
        {
            var appId = await apps.CreateAppAsync(
                request,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", appId.ToString(), null, updatedValue, true,
                "Az alkalmazás létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/apps/{appId}",
                new
                {
                    appId,
                    request.Code,
                    request.AppName,
                    isActive = true,
                    message = "Az alkalmazás létrejött."
                });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", null, null, updatedValue, false,
                "Ez az alkalmazáskód már létezik.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez az alkalmazáskód már létezik."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt az alkalmazás létrehozásakor. Code: {Code}",
                request.Code);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", null, null, updatedValue, false,
                "Az alkalmazás létrehozása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "Az alkalmazás létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAppAsync(
        int appId,
        UpdateAppRequest request,
        AppMaintenanceRepository apps,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AppMaintenanceEndpoints");
        var objectId = appId.ToString();
        var updatedValue =
            $"Code={request.Code}; AppName={request.AppName}; IsActive={request.IsActive}";

        if (appId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", objectId, null, updatedValue, false,
                "Érvénytelen alkalmazásazonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen alkalmazásazonosító."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Code) ||
            request.Code.Length > 50)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", objectId, null, updatedValue, false,
                "Az alkalmazáskód érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Az alkalmazáskód nem lehet üres, és legfeljebb 50 karakter lehet."
            });
        }

        if (string.IsNullOrWhiteSpace(request.AppName) ||
            request.AppName.Length > 100)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", objectId, null, updatedValue, false,
                "Az alkalmazás neve érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Az alkalmazás neve nem lehet üres, és legfeljebb 100 karakter lehet."
            });
        }

        string? originalValue = null;

        try
        {
            var existingApps = await apps.GetAppsAsync(
                search: null,
                isActive: null,
                cancellationToken);

            var originalApp = existingApps
                .FirstOrDefault(item => item.AppId == appId);

            if (originalApp is not null)
            {
                originalValue =
                    $"Code={originalApp.Code}; " +
                    $"AppName={originalApp.AppName}; " +
                    $"IsActive={originalApp.IsActive}";
            }

            await apps.UpdateAppAsync(
                appId,
                request,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", objectId, originalValue, updatedValue, true,
                "Az alkalmazás adatai módosultak.",
                cancellationToken);

            return Results.Ok(new
            {
                appId,
                request.Code,
                request.AppName,
                request.IsActive,
                message = "Az alkalmazás adatai módosultak."
            });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", objectId, originalValue, updatedValue, false,
                "Ez az alkalmazáskód már használatban van.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez az alkalmazáskód már használatban van."
            });
        }
        catch (SqlException exception) when (exception.Number == 50011)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", objectId, originalValue, updatedValue, false,
                "Az alapalkalmazások adatai nem módosíthatók.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Az alapalkalmazások adatai nem módosíthatók."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt az alkalmazás módosításakor. AppId: {AppId}",
                appId);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", objectId, originalValue, updatedValue, false,
                "Az alkalmazás módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "Az alkalmazás módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteAuditSafelyAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        ILogger logger,
        string operation,
        string? objectId,
        string? originalValue,
        string? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                operation,
                "App",
                objectId,
                originalValue,
                updatedValue,
                success,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Az auditbejegyzés fájlba írása sikertelen. " +
                "Művelet: {Operation}, AppId: {AppId}",
                operation,
                objectId);
        }
    }
}