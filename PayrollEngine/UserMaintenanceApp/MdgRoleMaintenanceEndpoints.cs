using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.UserMaintenanceApp;

public static class MdgRoleMaintenanceEndpoints
{
    public static WebApplication MapMdgRoleMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPut(
                "/api/roles/{roleId:int}/master-data-groups",
                SetPermissionsAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("SetMdgRolePermissions");

        app.MapGet(
                "/api/roles/{roleId:int}/master-data-groups",
                GetPermissionsAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("GetMdgRolePermissions");

        return app;
    }

    private static async Task<IResult> SetPermissionsAsync(
        int roleId,
        SetMdgRolePermissionsRequest request,
        MdgRoleMaintenanceRepository permissions,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MdgRoleMaintenanceEndpoints");

        var objectId =
            $"RoleId={roleId};MasterDataGroupId={request.MasterDataGroupId}";

        var updatedValue =
            $"CanRead={request.CanRead}; CanEdit={request.CanEdit}";

        if (roleId <= 0 || request.MasterDataGroupId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                null,
                updatedValue,
                false,
                "Érvénytelen szerep- vagy törzsadatcsoport-azonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen szerep- vagy törzsadatcsoport-azonosító."
            });
        }

        if (request.CanEdit && !request.CanRead)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                null,
                updatedValue,
                false,
                "Szerkesztési jog csak olvasási joggal együtt adható.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Szerkesztési jog csak olvasási joggal együtt adható."
            });
        }

        string? originalValue = null;

        try
        {
            var existingPermissions =
                await permissions.GetPermissionsAsync(
                    roleId,
                    cancellationToken);

            var originalPermission = existingPermissions
                .FirstOrDefault(item =>
                    item.MasterDataGroupId == request.MasterDataGroupId);

            if (originalPermission is not null)
            {
                originalValue =
                    $"CanRead={originalPermission.CanRead}; " +
                    $"CanEdit={originalPermission.CanEdit}";
            }

            await permissions.SetPermissionsAsync(
                roleId,
                request,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                originalValue,
                updatedValue,
                true,
                "A törzsadatcsoport jogosultságai módosultak.",
                cancellationToken);

            return Results.Ok(new
            {
                roleId,
                request.MasterDataGroupId,
                request.CanRead,
                request.CanEdit,
                message = "A törzsadatcsoport jogosultságai módosultak."
            });
        }
        catch (SqlException exception) when (exception.Number == 50030)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                originalValue,
                updatedValue,
                false,
                "Az aktív szerep nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "Az aktív szerep nem található."
            });
        }
        catch (SqlException exception) when (exception.Number == 50031)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                originalValue,
                updatedValue,
                false,
                "A törzsadatcsoport nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A törzsadatcsoport nem található."
            });
        }
        catch (SqlException exception) when (exception.Number == 50032)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                originalValue,
                updatedValue,
                false,
                "Szerkesztési jog csak olvasási joggal együtt adható.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Szerkesztési jog csak olvasási joggal együtt adható."
            });
        }
        catch (SqlException exception) when (exception.Number == 50033)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                originalValue,
                updatedValue,
                false,
                "A szerep nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A szerep nem található."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a törzsadatcsoport-jogosultság beállításakor. " +
                "RoleId: {RoleId}, MasterDataGroupId: {MasterDataGroupId}",
                roleId,
                request.MasterDataGroupId);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                objectId,
                originalValue,
                updatedValue,
                false,
                "A törzsadatcsoport-jogosultság beállítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A törzsadatcsoport-jogosultság beállítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetPermissionsAsync(
        int roleId,
        MdgRoleMaintenanceRepository permissions,
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
            var result = await permissions.GetPermissionsAsync(
                roleId,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50033)
        {
            return Results.NotFound(new
            {
                message = "A szerep nem található."
            });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger(
                "MdgRoleMaintenanceEndpoints");

            logger.LogError(
                exception,
                "Hiba történt a szerep MDG-jogosultságainak lekérdezésekor. " +
                "RoleId: {RoleId}",
                roleId);

            return Results.Problem(
                title: "A törzsadatcsoport-jogosultságok lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteAuditSafelyAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        ILogger logger,
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
                "SET_MDG_ROLE_PERMISSIONS",
                "RoleMasterDataGroupPermission",
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
                "Kapcsolat: {ObjectId}",
                objectId);
        }
    }
}