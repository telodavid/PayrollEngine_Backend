using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.UserMaintenanceApp;

public static class RoleAssignMaintenanceEndpoints
{
    public static WebApplication MapRoleAssignMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/users/{userId:int}/roles",
                AssignRoleToUserAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("AssignRoleToUser");

        app.MapGet(
        "/api/users/{userId:int}/roles",
        GetUserRolesAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "USER_MAINTENANCE"))
    .WithName("GetUserRoles");

        app.MapDelete(
        "/api/users/{userId:int}/roles/{roleId:int}",
        DeleteRoleAssignmentAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "USER_MAINTENANCE"))
    .WithName("DeleteRoleAssignment");

        return app;
    }
    private static async Task<IResult> DeleteRoleAssignmentAsync(
    int userId,
    int roleId,
    DateOnly validFrom,
    RoleAssignMaintenanceRepository assignments,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    AuditLogService auditLog,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "RoleAssignMaintenanceEndpoints");

        var updatedValue = $"ValidFrom={validFrom:yyyy-MM-dd}";

        if (userId <= 0 || roleId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                userId, roleId, updatedValue, false,
                "Érvénytelen felhasználó- vagy szerepazonosító.",
                cancellationToken,
                 operation: "DELETE_ROLE_ASSIGNMENT");

            return Results.BadRequest(new
            {
                message = "Érvénytelen felhasználó- vagy szerepazonosító."
            });
        }

        try
        {
            await assignments.DeleteRoleAssignmentAsync(
                userId,
                roleId,
                validFrom,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                userId, roleId, updatedValue, true,
                "A szerep-hozzárendelés törlése sikerült.",
                cancellationToken,
                operation: "DELETE_ROLE_ASSIGNMENT");

            return Results.Ok(new
            {
                message = "A szerep-hozzárendelés törlése sikerült."
            });
        }
        catch (SqlException exception) when (exception.Number == 50700)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen felhasználó, szerep vagy érvényességi dátum."
            });
        }
        catch (SqlException exception) when (exception.Number == 50701)
        {
            return Results.NotFound(new
            {
                message = "A felhasználó nem található."
            });
        }
        catch (SqlException exception) when (exception.Number == 50702)
        {
            return Results.NotFound(new
            {
                message = "A szerep-hozzárendelés nem található."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a szerep-hozzárendelés törlésekor. " +
                "UserId: {UserId}, RoleId: {RoleId}",
                userId,
                roleId);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                userId, roleId, updatedValue, false,
                "A szerep-hozzárendelés törlése sikertelen.",
                cancellationToken,
                operation: "DELETE_ROLE_ASSIGNMENT");

            return Results.Problem(
                title: "A szerep-hozzárendelés törlése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    private static async Task<IResult> GetUserRolesAsync(
    int userId,
    RoleAssignMaintenanceRepository assignments,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    AuditLogService auditLog,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "RoleAssignMaintenanceEndpoints");

        if (userId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen felhasználó-azonosító."
            });
        }

        try
        {
            var result = await assignments.GetAssignmentsForUserAsync(
                userId,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50600)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen felhasználó-azonosító."
            });
        }
        catch (SqlException exception) when (exception.Number == 50601)
        {
            return Results.NotFound(new
            {
                message = "A felhasználó nem található."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a felhasználó szerepeinek lekérdezésekor. " +
                "UserId: {UserId}",
                userId);

            return Results.Problem(
                title: "A felhasználó szerepeinek lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    private static async Task<IResult> AssignRoleToUserAsync(
        int userId,
        AssignRoleToUserRequest request,
        RoleAssignMaintenanceRepository assignments,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "RoleAssignMaintenanceEndpoints");

        var validFrom = request.ValidFrom.ToString("yyyy-MM-dd");
        var validTo = request.ValidTo.HasValue
            ? request.ValidTo.Value.ToString("yyyy-MM-dd")
            : "null";

        var updatedValue =
            $"ValidFrom={validFrom}; ValidTo={validTo}";

        if (userId <= 0 || request.RoleId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                userId,
                request.RoleId,
                updatedValue,
                false,
                "Érvénytelen user- vagy szerepazonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen user- vagy szerepazonosító."
            });
        }

        if (request.ValidTo.HasValue &&
            request.ValidTo.Value < request.ValidFrom)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                userId,
                request.RoleId,
                updatedValue,
                false,
                "Az érvényesség vége nem lehet korábbi a kezdeténél.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Az érvényesség vége nem lehet korábbi a kezdeténél."
            });
        }

        try
        {
            await assignments.AssignRoleToUserAsync(
                userId,
                request.RoleId,
                request.ValidFrom,
                request.ValidTo,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                userId,
                request.RoleId,
                updatedValue,
                true,
                "A szerep hozzárendelése sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                userId,
                request.RoleId,
                request.ValidFrom,
                request.ValidTo,
                message = "A szerep hozzárendelése sikerült."
            });
        }
        catch (SqlException exception) when (exception.Number == 50004)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                userId,
                request.RoleId,
                updatedValue,
                false,
                "Érvénytelen szerep-hozzárendelési időszak.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen szerep-hozzárendelési időszak."
            });
        }
        catch (SqlException exception) when (exception.Number == 50007)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                userId,
                request.RoleId,
                updatedValue,
                false,
                "A szerep nem található vagy inaktív.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "A szerep nem található vagy inaktív."
            });
        }
        catch (SqlException exception) when (exception.Number == 547)
        {
            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                userId,
                request.RoleId,
                updatedValue,
                false,
                "A felhasználó nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A felhasználó nem található."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a szerep hozzárendelésekor. " +
                "UserId: {UserId}, RoleId: {RoleId}",
                userId,
                request.RoleId);

            await WriteAuditSafelyAsync(
                httpContext,
                auditLog,
                logger,
                userId,
                request.RoleId,
                updatedValue,
                false,
                "A szerep hozzárendelése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A szerep hozzárendelése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteAuditSafelyAsync(
     HttpContext httpContext,
     AuditLogService auditLog,
     ILogger logger,
     int userId,
     int roleId,
     string? updatedValue,
     bool success,
     string message,
     CancellationToken cancellationToken,
     string operation = "ASSIGN_ROLE")
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                operation,
                "UserRole",
                $"UserId={userId};RoleId={roleId}",
                null,
                updatedValue,
                success,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Az auditbejegyzés írása sikertelen. " +
                "UserId: {UserId}, RoleId: {RoleId}",
                userId,
                roleId);
        }
    }
}