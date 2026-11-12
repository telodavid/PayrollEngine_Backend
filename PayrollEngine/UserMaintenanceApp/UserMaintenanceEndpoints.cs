using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.UserMaintenanceApp;

public static class UserMaintenanceEndpoints
{
    public static WebApplication MapUserMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/users/{userId:int}/password",
                SetPasswordAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("SetUserPassword");

        app.MapPost("/api/users", CreateUserAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("CreateUser");

        app.MapPut("/api/users/{userId:int}", UpdateUserAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "USER_MAINTENANCE"))
            .WithName("UpdateUser");
        app.MapGet("/api/users", SearchUsersAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "USER_MAINTENANCE"))
    .WithName("SearchUsers");

        return app;
    }
    private static async Task<IResult> SearchUsersAsync(
    string? search,
    bool? isActive,
    UserMaintenanceRepository users,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
    {
        try
        {
            var result = await users.SearchUsersAsync(
                search,
                isActive,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            loggerFactory
                .CreateLogger("UserMaintenanceEndpoints")
                .LogError(exception, "Hiba történt a felhasználók lekérdezésekor.");

            return Results.Problem(
                title: "A felhasználók lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> SetPasswordAsync(
        int userId,
        SetUserPasswordRequest request,
        UserMaintenanceRepository users,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("UserMaintenanceEndpoints");

        if (userId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "SET_PASSWORD", "User", userId.ToString(),
                null, null, false,
                "Érvénytelen felhasználó-azonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen felhasználó-azonosító."
            });
        }

        var loggedInUserIdValue = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(loggedInUserIdValue, out var loggedInUserId))
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "SET_PASSWORD", "User", userId.ToString(),
                null, null, false,
                "A bejelentkezett felhasználó azonosítója nem található.",
                cancellationToken);

            return Results.Json(
                new
                {
                    message = "A bejelentkezett felhasználó azonosítója nem található."
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (loggedInUserId == userId)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "SET_PASSWORD", "User", userId.ToString(),
                null, null, false,
                "A felhasználó a saját jelszavát próbálta módosítani.",
                cancellationToken);

            return Results.Json(
                new
                {
                    message = "A bejelentkezett felhasználó saját jelszavát nem módosíthatja."
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length > 255)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "SET_PASSWORD", "User", userId.ToString(),
                null, null, false,
                "A megadott jelszó érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A jelszó nem lehet üres, és legfeljebb 255 karakter lehet."
            });
        }

        try
        {
            await users.SetPasswordAsync(
                userId,
                request.NewPassword,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "SET_PASSWORD", "User", userId.ToString(),
                null,
                "Jelszó módosítva; az érték nincs naplózva.",
                true,
                "A felhasználó jelszava sikeresen módosult.",
                cancellationToken);

            return Results.Ok(new
            {
                message = "A felhasználó jelszava sikeresen módosult."
            });
        }
        catch (SqlException exception) when (exception.Number == 50002)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "SET_PASSWORD", "User", userId.ToString(),
                null, null, false,
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
                "Hiba történt a jelszó beállításakor. UserId: {UserId}",
                userId);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "SET_PASSWORD", "User", userId.ToString(),
                null, null, false,
                "A jelszó beállítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A jelszó beállítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        UserMaintenanceRepository users,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("UserMaintenanceEndpoints");

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "User", null,
                null, null, false,
                "A felhasználónév nem lehet üres.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A felhasználónév nem lehet üres."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length > 255)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "User", null,
                null, $"UserName={request.UserName}", false,
                "A megadott jelszó érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A jelszó nem lehet üres, és legfeljebb 255 karakter lehet."
            });
        }

        try
        {
            var userId = await users.CreateUserAsync(
                request.UserName,
                request.Password,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "User", userId.ToString(),
                null,
                $"UserName={request.UserName}; IsActive=true",
                true,
                "A felhasználó létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/users/{userId}",
                new
                {
                    userId,
                    userName = request.UserName,
                    message = "A felhasználó létrejött."
                });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "User", null,
                null, $"UserName={request.UserName}", false,
                "Ez a felhasználónév már létezik.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez a felhasználónév már létezik."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a felhasználó létrehozásakor.");

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "CREATE", "User", null,
                null, $"UserName={request.UserName}", false,
                "A felhasználó létrehozása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A felhasználó létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateUserAsync(
        int userId,
        UpdateUserRequest request,
        UserMaintenanceRepository users,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("UserMaintenanceEndpoints");

        if (userId <= 0)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null, null, false,
                "Érvénytelen felhasználó-azonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen felhasználó-azonosító."
            });
        }

        if (string.IsNullOrWhiteSpace(request.UserName) ||
            request.UserName.Length > 100)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null, null, false,
                "A megadott felhasználónév érvénytelen.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "A felhasználónév nem lehet üres, és legfeljebb 100 karakter lehet."
            });
        }

        var currentUserIdValue = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(currentUserIdValue, out var currentUserId))
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null, null, false,
                "A bejelentkezett felhasználó azonosítója nem található.",
                cancellationToken);

            return Results.Unauthorized();
        }

        try
        {
            await users.UpdateUserAsync(
                userId,
                request.UserName,
                request.IsActive,
                currentUserId,
                cancellationToken);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null,
                $"UserName={request.UserName}; IsActive={request.IsActive}",
                true,
                "A felhasználó adatai módosultak.",
                cancellationToken);

            return Results.Ok(new
            {
                userId,
                userName = request.UserName,
                isActive = request.IsActive,
                message = "A felhasználó adatai módosultak."
            });
        }
        catch (SqlException exception) when (exception.Number == 50009)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null, null, false,
                "A bejelentkezett felhasználó saját fiókját nem inaktiválhatja.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "A bejelentkezett felhasználó saját fiókját nem inaktiválhatja."
            });
        }
        catch (SqlException exception) when (exception.Number == 50002)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null, null, false,
                "A felhasználó nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A felhasználó nem található."
            });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null, $"UserName={request.UserName}", false,
                "Ez a felhasználónév már használatban van.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez a felhasználónév már használatban van."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a felhasználó módosításakor. UserId: {UserId}",
                userId);

            await WriteAuditSafelyAsync(
                httpContext, auditLog, logger,
                "UPDATE", "User", userId.ToString(),
                null,
                $"UserName={request.UserName}; IsActive={request.IsActive}",
                false,
                "A felhasználó módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A felhasználó módosítása sikertelen.",
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
                "Az auditbejegyzés fájlba írása sikertelen. Művelet: {Operation}, objektum: {ObjectType}, azonosító: {ObjectId}",
                operation,
                objectType,
                objectId);
        }
    }
}