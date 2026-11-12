using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.UserMaintenanceApp;

public static class AppToRoleMaintenanceEndpoints
{
    public static WebApplication MapAppToRoleMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/roles/{roleId:int}/apps",
                AssignAppToRoleAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("AssignAppToRole");

        app.MapGet(
                "/api/roles/{roleId:int}/apps",
                GetAppsForRoleAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("GetAppsForRole");

        app.MapDelete(
                "/api/roles/{roleId:int}/apps/{appId:int}",
                RemoveAppFromRoleAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("RemoveAppFromRole");

        return app;
    }

    private static async Task<IResult> AssignAppToRoleAsync(
        int roleId,
        AssignAppToRoleRequest request,
        AppToRoleMaintenanceRepository assignments,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "AppToRoleMaintenanceEndpoints");

        var objectId = $"RoleId={roleId};AppId={request.AppId}";
        var updatedValue = objectId;

        if (roleId <= 0 || request.AppId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "ASSIGN_APP_TO_ROLE",
                objectId,
                null,
                updatedValue,
                false,
                "Érvénytelen szerep- vagy alkalmazásazonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen szerep- vagy alkalmazásazonosító."
            });
        }

        try
        {
            await assignments.AssignAppToRoleAsync(
                roleId,
                request.AppId,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "ASSIGN_APP_TO_ROLE",
                objectId,
                null,
                updatedValue,
                true,
                "Az alkalmazás hozzárendelése a szerephez sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                roleId,
                appId = request.AppId,
                message = "Az alkalmazás hozzárendelése a szerephez sikerült."
            });
        }
        catch (SqlException exception) when (exception.Number == 50020)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "ASSIGN_APP_TO_ROLE",
                objectId,
                null,
                updatedValue,
                false,
                "Az aktív szerep nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "Az aktív szerep nem található."
            });
        }
        catch (SqlException exception) when (exception.Number == 50021)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "ASSIGN_APP_TO_ROLE",
                objectId,
                null,
                updatedValue,
                false,
                "Az aktív alkalmazás nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "Az aktív alkalmazás nem található."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt az alkalmazás szerephez rendelésekor. " +
                "RoleId: {RoleId}, AppId: {AppId}",
                roleId,
                request.AppId);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "ASSIGN_APP_TO_ROLE",
                objectId,
                null,
                updatedValue,
                false,
                "Az alkalmazás hozzárendelése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "Az alkalmazás hozzárendelése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetAppsForRoleAsync(
        int roleId,
        AppToRoleMaintenanceRepository assignments,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (roleId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen szerepazonosító."
            });
        }

        try
        {
            var apps = await assignments.GetAppsForRoleAsync(
                roleId,
                cancellationToken);

            return Results.Ok(apps);
        }
        catch (SqlException exception) when (exception.Number == 50024)
        {
            return Results.NotFound(new
            {
                message = "A szerep nem található."
            });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger(
                "AppToRoleMaintenanceEndpoints");

            logger.LogError(
                exception,
                "Hiba történt a szerephez rendelt alkalmazások lekérdezésekor. " +
                "RoleId: {RoleId}",
                roleId);

            return Results.Problem(
                title: "A szerep alkalmazásainak lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> RemoveAppFromRoleAsync(
        int roleId,
        int appId,
        AppToRoleMaintenanceRepository assignments,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "AppToRoleMaintenanceEndpoints");

        var objectId = $"RoleId={roleId};AppId={appId}";
        var originalValue = objectId;

        if (roleId <= 0 || appId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "REMOVE_APP_FROM_ROLE",
                objectId,
                originalValue,
                null,
                false,
                "Érvénytelen szerep- vagy alkalmazásazonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen szerep- vagy alkalmazásazonosító."
            });
        }

        try
        {
            await assignments.RemoveAppFromRoleAsync(
                roleId,
                appId,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "REMOVE_APP_FROM_ROLE",
                objectId,
                originalValue,
                null,
                true,
                "Az alkalmazás hozzárendelését visszavontuk.",
                cancellationToken);

            return Results.Ok(new
            {
                roleId,
                appId,
                message = "Az alkalmazás hozzárendelését visszavontuk."
            });
        }
        catch (SqlException exception) when (exception.Number == 50025)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "REMOVE_APP_FROM_ROLE",
                objectId,
                originalValue,
                null,
                false,
                "A szerep nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A szerep nem található."
            });
        }
        catch (SqlException exception) when (exception.Number == 50026)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "REMOVE_APP_FROM_ROLE",
                objectId,
                originalValue,
                null,
                false,
                "Az alkalmazás nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "Az alkalmazás nem található."
            });
        }
        catch (SqlException exception) when (exception.Number == 50027)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "REMOVE_APP_FROM_ROLE",
                objectId,
                originalValue,
                null,
                false,
                "Az alkalmazás nincs ehhez a szerephez rendelve.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "Ez az alkalmazás nincs ehhez a szerephez rendelve."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt az alkalmazás szereptől való elvételénél. " +
                "RoleId: {RoleId}, AppId: {AppId}",
                roleId,
                appId);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                "REMOVE_APP_FROM_ROLE",
                objectId,
                originalValue,
                null,
                false,
                "Az alkalmazás hozzárendelésének visszavonása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "Az alkalmazás hozzárendelésének visszavonása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteAuditSafelyAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        ILogger logger,
        string operation,
        string objectId,
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
                "RoleApp",
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
                "Művelet: {Operation}, kapcsolat: {ObjectId}",
                operation,
                objectId);
        }
    }
}