using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.AbsenceTypeMaintenanceApp;

public static class AbsenceTypeEndpoints
{
    public static WebApplication MapAbsenceTypeEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/absence-types", SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "ABSENCE_TYPE_MAINTENANCE"))
            .WithName("SearchAbsenceTypes");

        app.MapGet("/api/absence-types/{absenceTypeId:int}", GetByIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "ABSENCE_TYPE_MAINTENANCE"))
            .WithName("GetAbsenceType");

        app.MapPost("/api/absence-types", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "ABSENCE_TYPE_MAINTENANCE"))
            .WithName("CreateAbsenceType");

        app.MapPut("/api/absence-types/{absenceTypeId:int}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "ABSENCE_TYPE_MAINTENANCE"))
            .WithName("UpdateAbsenceType");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        bool? isPaid,
        bool? isActive,
        AbsenceTypeRepository absenceTypes,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AbsenceTypeEndpoints");

        try
        {
            var result = await absenceTypes.SearchAsync(
                search,
                isPaid,
                isActive,
                cancellationToken);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(result),
                success: true,
                message: "A távolléttípusok lekérdezése sikeres.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távolléttípusok lekérdezésekor.");

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
                objectId: null,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A távolléttípusok lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A távolléttípusok lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetByIdAsync(
        int absenceTypeId,
        AbsenceTypeRepository absenceTypes,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AbsenceTypeEndpoints");

        if (absenceTypeId <= 0)
        {
            const string message = "Érvénytelen távolléttípus-azonosító.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var result = await absenceTypes.GetByIdAsync(
                absenceTypeId,
                cancellationToken);

            if (result is null)
            {
                const string message = "A távolléttípus nem található.";

                await WriteAuditSafelyAsync(
                    auditLog,
                    httpContext,
                    logger,
                    operation: "READ",
                    objectId: absenceTypeId.ToString(),
                    originalValue: null,
                    updatedValue: null,
                    success: false,
                    message,
                    cancellationToken);

                return Results.NotFound(new { message });
            }

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(result),
                success: true,
                message: "A távolléttípus lekérdezése sikeres.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50084)
        {
            const string message = "Érvénytelen távolléttípus-azonosító.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50085)
        {
            const string message = "A távolléttípus nem található.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távolléttípus lekérdezésekor. AbsenceTypeId: {AbsenceTypeId}",
                absenceTypeId);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A távolléttípus lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A távolléttípus lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateAsync(
        CreateAbsenceTypeRequest request,
        AbsenceTypeRepository absenceTypes,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AbsenceTypeEndpoints");
        var validationError = ValidateRequest(request.Code, request.Name);

        if (validationError is not null)
        {
            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var absenceTypeId = await absenceTypes.CreateAsync(
                request,
                cancellationToken);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(new
                {
                    request.Code,
                    request.Name,
                    request.IsPaid,
                    IsActive = true
                }),
                success: true,
                message: "A távolléttípus létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/absence-types/{absenceTypeId}",
                new
                {
                    absenceTypeId,
                    request.Code,
                    request.Name,
                    request.IsPaid,
                    isActive = true,
                    message = "A távolléttípus létrejött."
                });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            const string message = "Ez a távolléttípus-kód már létezik.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return Results.Conflict(new { message });
        }
        catch (SqlException exception)
            when (exception.Number is >= 50080 and <= 50083)
        {
            const string message = "A távolléttípus adatai érvénytelenek.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távolléttípus létrehozásakor. Code: {Code}",
                request.Code);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A távolléttípus létrehozása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A távolléttípus létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int absenceTypeId,
        UpdateAbsenceTypeRequest request,
        AbsenceTypeRepository absenceTypes,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("AbsenceTypeEndpoints");

        if (absenceTypeId <= 0)
        {
            const string message = "Érvénytelen távolléttípus-azonosító.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        var validationError = ValidateRequest(request.Code, request.Name);

        if (validationError is not null)
        {
            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        AbsenceTypeModel? original;

        try
        {
            original = await absenceTypes.GetByIdAsync(
                absenceTypeId,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50084)
        {
            const string message = "Érvénytelen távolléttípus-azonosító.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50085)
        {
            const string message = "A távolléttípus nem található.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távolléttípus módosítás előtti lekérdezésekor. AbsenceTypeId: {AbsenceTypeId}",
                absenceTypeId);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A távolléttípus módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A távolléttípus módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (original is null)
        {
            const string message = "A távolléttípus nem található.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }

        try
        {
            await absenceTypes.UpdateAsync(
                absenceTypeId,
                request,
                cancellationToken);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: true,
                message: "A távolléttípus adatai módosultak.",
                cancellationToken);

            return Results.Ok(new
            {
                absenceTypeId,
                request.Code,
                request.Name,
                request.IsPaid,
                request.IsActive,
                message = "A távolléttípus adatai módosultak."
            });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            const string message = "Ez a távolléttípus-kód már használatban van.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return Results.Conflict(new { message });
        }
        catch (SqlException exception)
            when (exception.Number is >= 50080 and <= 50085)
        {
            const string message =
                "A távolléttípus adatai érvénytelenek, vagy a távolléttípus nem található.";

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);

            return exception.Number == 50085
                ? Results.NotFound(new { message })
                : Results.BadRequest(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távolléttípus módosításakor. AbsenceTypeId: {AbsenceTypeId}",
                absenceTypeId);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: absenceTypeId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A távolléttípus módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A távolléttípus módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? ValidateRequest(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 30)
        {
            return "A kód nem lehet üres, és legfeljebb 30 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return "A név nem lehet üres, és legfeljebb 100 karakter lehet.";
        }

        return null;
    }

    private static async Task WriteAuditSafelyAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
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
                "AbsenceType",
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
                "A távolléttípus auditnapló-bejegyzésének mentése sikertelen. Operation: {Operation}, ObjectId: {ObjectId}",
                operation,
                objectId);
        }
    }
}