using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.UserMaintenanceApp;

public static class RoleMaintenanceEndpoints
{
    public static WebApplication MapRoleMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPost("/api/roles", CreateRoleAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("CreateRole");

        app.MapGet("/api/roles", GetRolesAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("GetRoles");

        app.MapPut("/api/roles/{roleId:int}", UpdateRoleAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("UpdateRole");

        return app;
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        RoleMaintenanceRepository roles,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RoleMaintenanceEndpoints");

        if (string.IsNullOrWhiteSpace(request.Code) ||
            request.Code.Length > 50)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "Role", null, null,
                $"Code={request.Code}; RoleName={request.RoleName}",
                false,
                "A szerepkód érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A szerepkód nem lehet üres, és legfeljebb 50 karakter lehet."
            });
        }

        if (string.IsNullOrWhiteSpace(request.RoleName) ||
            request.RoleName.Length > 100)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "Role", null, null,
                $"Code={request.Code}; RoleName={request.RoleName}",
                false,
                "A szerep neve érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A szerep neve nem lehet üres, és legfeljebb 100 karakter lehet."
            });
        }

        try
        {
            var roleId = await roles.CreateRoleAsync(
                request.Code,
                request.RoleName,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "Role", roleId.ToString(), null,
                $"Code={request.Code}; RoleName={request.RoleName}; IsActive=true",
                true,
                "A szerep létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/roles/{roleId}",
                new
                {
                    roleId,
                    request.Code,
                    request.RoleName,
                    message = "A szerep létrejött."
                });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "Role", null, null,
                $"Code={request.Code}; RoleName={request.RoleName}",
                false,
                "Ez a szerepkód már létezik.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez a szerepkód már létezik."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a szerep létrehozásakor. Code: {Code}",
                request.Code);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "Role", null, null,
                $"Code={request.Code}; RoleName={request.RoleName}",
                false,
                "A szerep létrehozása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A szerep létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetRolesAsync(
        string? search,
        bool? isActive,
        RoleMaintenanceRepository roles,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RoleMaintenanceEndpoints");

        try
        {
            var result = await roles.GetRolesAsync(
                search,
                isActive,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a szerepek lekérdezésekor.");

            return Results.Problem(
                title: "A szerepek lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateRoleAsync(
        int roleId,
        UpdateRoleRequest request,
        RoleMaintenanceRepository roles,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RoleMaintenanceEndpoints");

        if (roleId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null, null,
                false,
                "Érvénytelen szerepazonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen szerepazonosító."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Code) ||
            request.Code.Length > 50)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null,
                $"Code={request.Code}; RoleName={request.RoleName}; IsActive={request.IsActive}",
                false,
                "A szerepkód érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A szerepkód nem lehet üres, és legfeljebb 50 karakter lehet."
            });
        }

        if (string.IsNullOrWhiteSpace(request.RoleName) ||
            request.RoleName.Length > 100)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null,
                $"Code={request.Code}; RoleName={request.RoleName}; IsActive={request.IsActive}",
                false,
                "A szerep neve érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A szerep neve nem lehet üres, és legfeljebb 100 karakter lehet."
            });
        }

        var userIdValue = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdValue, out var currentUserId))
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null, null,
                false,
                "A bejelentkezett felhasználó azonosítója nem található.",
                cancellationToken);

            return Results.Unauthorized();
        }

        try
        {
            var existingRoles = await roles.GetRolesAsync(
                search: null,
                isActive: null,
                cancellationToken);

            var originalRole = existingRoles
                .FirstOrDefault(role => role.RoleId == roleId);

            var originalValue = originalRole is null
                ? null
                : $"Code={originalRole.Code}; " +
                  $"RoleName={originalRole.RoleName}; " +
                  $"IsActive={originalRole.IsActive}";

            var updatedValue =
                $"Code={request.Code}; " +
                $"RoleName={request.RoleName}; " +
                $"IsActive={request.IsActive}";

            await roles.UpdateRoleAsync(
                roleId,
                request.Code,
                request.RoleName,
                request.IsActive,
                currentUserId,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(),
                originalValue, updatedValue,
                true,
                "A szerep módosítása sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                roleId,
                message = "A szerep módosítása sikerült."
            });
        }
        catch (SqlException exception) when (exception.Number == 50006)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null,
                $"Code={request.Code}; RoleName={request.RoleName}; IsActive={request.IsActive}",
                false,
                "A bejelentkezett felhasználó saját aktív szerepe nem tehető inaktívvá.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "A bejelentkezett felhasználó saját aktív szerepe nem tehető inaktívvá."
            });
        }
        catch (SqlException exception) when (exception.Number == 50003)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null,
                $"Code={request.Code}; RoleName={request.RoleName}; IsActive={request.IsActive}",
                false,
                "A szerep nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A szerep nem található."
            });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null,
                $"Code={request.Code}; RoleName={request.RoleName}; IsActive={request.IsActive}",
                false,
                "Ez a szerepkód már egy másik szerephez tartozik.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez a szerepkód már egy másik szerephez tartozik."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a szerep módosításakor. RoleId: {RoleId}",
                roleId);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "Role", roleId.ToString(), null,
                $"Code={request.Code}; RoleName={request.RoleName}; IsActive={request.IsActive}",
                false,
                "A szerep módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A szerep módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteAuditSafelyAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        ILogger logger,
        string operation,
        string objectType,
        string? objectId,
        string? originalValue,
        string? updatedValue,
        bool success,
        string? message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                operation,
                objectType,
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
                "Művelet: {Operation}, objektum: {ObjectType}, azonosító: {ObjectId}",
                operation,
                objectType,
                objectId);
        }
    }
}