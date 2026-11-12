using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.PayrollElementMaintenanceApp;

public static class PayrollElementFieldValueEndpoints
{
    public static WebApplication MapPayrollElementFieldValueEndpoints(
        this WebApplication app)
    {
        app.MapGet(
                "/api/payroll-elements/{payrollElementId:int}/field-values",
                GetAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
            .WithName("GetPayrollElementFieldValues");

        app.MapPost(
                "/api/payroll-elements/{payrollElementId:int}/field-values",
                SaveAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
            .WithName("SavePayrollElementFieldValue");
        app.MapPut(
        "/api/payroll-elements/{payrollElementId:int}/field-values/{fieldId:int}/end",
        EndAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
    .WithName("EndPayrollElementFieldValue");

        app.MapGet(
        "/api/payroll-elements/{payrollElementId:int}/field-values/{fieldId:int}/history",
        GetHistoryAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
    .WithName("GetPayrollElementFieldValueHistory");

        return app;
    }
    private static async Task<IResult> GetHistoryAsync(
    int payrollElementId,
    int fieldId,
    HttpContext httpContext,
    PayrollElementMaintenanceRepository payrollElements,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
        {
            return Results.Unauthorized();
        }

        if (payrollElementId <= 0 || fieldId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen bérelem- vagy tulajdonságazonosító."
            });
        }

        try
        {
            var history = await payrollElements.GetFieldValueHistoryAsync(
                payrollElementId,
                fieldId,
                userId,
                cancellationToken);

            return Results.Ok(history);
        }
        catch (SqlException exception)
        {
            var (statusCode, message) = exception.Number switch
            {
                50500 => (
                    StatusCodes.Status400BadRequest,
                    "Érvénytelen bérelem-, tulajdonság- vagy felhasználó-azonosító."),
                50501 or 50504 => (
                    StatusCodes.Status403Forbidden,
                    "Nincs jogosultság az előzmények megtekintéséhez."),
                50502 or 50503 => (
                    StatusCodes.Status404NotFound,
                    "A bérelem vagy a tulajdonság nem található."),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "Az előzmények lekérdezése sikertelen.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                loggerFactory
                    .CreateLogger("PayrollElementFieldValueEndpoints")
                    .LogError(
                        exception,
                        "Hiba az előzmények lekérdezésekor. Bérelem: {PayrollElementId}, tulajdonság: {FieldId}",
                        payrollElementId,
                        fieldId);
            }

            return Results.Json(new { message }, statusCode: statusCode);
        }
        catch (Exception exception)
        {
            loggerFactory
                .CreateLogger("PayrollElementFieldValueEndpoints")
                .LogError(
                    exception,
                    "Hiba az előzmények lekérdezésekor. Bérelem: {PayrollElementId}, tulajdonság: {FieldId}",
                    payrollElementId,
                    fieldId);

            return Results.Json(
                new { message = "Az előzmények lekérdezése sikertelen." },
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    private static async Task<IResult> EndAsync(
    int payrollElementId,
    int fieldId,
    EndPayrollElementFieldValueRequest request,
    HttpContext httpContext,
    PayrollElementMaintenanceRepository payrollElements,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
    {
        var userId = GetUserId(httpContext);
        var objectId = $"{payrollElementId}/{fieldId}";
        var updatedValue = $"ValidTo={request.ValidTo:yyyy-MM-dd}";

        if (userId is null)
        {
            return await ErrorAsync(
                auditLog,
                httpContext,
                "END",
                objectId,
                "A bejelentkezett felhasználó azonosítója hiányzik.",
                StatusCodes.Status401Unauthorized,
                cancellationToken,
                updatedValue);
        }

        if (payrollElementId <= 0 || fieldId <= 0)
        {
            return await ErrorAsync(
                auditLog,
                httpContext,
                "END",
                objectId,
                "Érvénytelen bérelem- vagy mezőazonosító.",
                StatusCodes.Status400BadRequest,
                cancellationToken,
                updatedValue);
        }

        try
        {
            var result = await payrollElements.EndFieldValueAsync(
                payrollElementId,
                fieldId,
                userId.Value,
                request,
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                "END",
                objectId,
                updatedValue,
                true,
                "A bérelem mezőértékének lezárása sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                result.PayrollElementId,
                result.FieldId,
                result.FieldValue,
                result.OptionId,
                result.ValidFrom,
                result.ValidTo,
                message = "A bérelem mezőértékének lezárása sikerült."
            });
        }
        catch (SqlException exception)
        {
            var (statusCode, message) = exception.Number switch
            {
                50540 => (StatusCodes.Status400BadRequest,
                    "Érvénytelen bérelem-, mező- vagy felhasználó-azonosító."),
                50541 or 50542 => (StatusCodes.Status403Forbidden,
                    "A felhasználó inaktív, vagy nincs szerkesztési joga ehhez a mezőcsoporthoz."),
                50543 or 50544 => (StatusCodes.Status404NotFound,
                    "A mező vagy az adott napon érvényes mezőérték nem található."),
                _ => (StatusCodes.Status500InternalServerError,
                    "A bérelem mezőértékének lezárása sikertelen.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                loggerFactory.CreateLogger("PayrollElementFieldValueEndpoints")
                    .LogError(
                        exception,
                        "Hiba történt a bérelem mezőértékének lezárásakor. PayrollElementId: {PayrollElementId}, FieldId: {FieldId}",
                        payrollElementId,
                        fieldId);
            }

            return await ErrorAsync(
                auditLog,
                httpContext,
                "END",
                objectId,
                message,
                statusCode,
                cancellationToken,
                updatedValue);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollElementFieldValueEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a bérelem mezőértékének lezárásakor. PayrollElementId: {PayrollElementId}, FieldId: {FieldId}",
                    payrollElementId,
                    fieldId);

            return await ErrorAsync(
                auditLog,
                httpContext,
                "END",
                objectId,
                "A bérelem mezőértékének lezárása sikertelen.",
                StatusCodes.Status500InternalServerError,
                cancellationToken,
                updatedValue);
        }
    }
    private static async Task<IResult> GetAsync(
        int payrollElementId,
        DateOnly? asOfDate,
        HttpContext httpContext,
        PayrollElementMaintenanceRepository payrollElements,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(httpContext);

        if (userId is null)
        {
            await WriteAuditAsync(
                auditLog, httpContext, "READ", payrollElementId.ToString(),
                null, false, "A bejelentkezett felhasználó azonosítója hiányzik.",
                cancellationToken);

            return Results.Unauthorized();
        }

        if (payrollElementId <= 0)
        {
            return await ErrorAsync(
                auditLog, httpContext, "READ", payrollElementId.ToString(),
                "Érvénytelen bérelem-azonosító.",
                StatusCodes.Status400BadRequest,
                cancellationToken);
        }

        try
        {
            var groups = await payrollElements.GetFieldValuesAsync(
                payrollElementId,
                userId.Value,
                asOfDate,
                cancellationToken);

            var fieldCount = groups.Sum(group => group.Fields.Count);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                "READ",
                payrollElementId.ToString(),
                $"GroupCount={groups.Count}; FieldCount={fieldCount}; AsOfDate={asOfDate?.ToString("yyyy-MM-dd") ?? "CURRENT"}",
                true,
                "A bérelem dinamikus mezőinek lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(groups);
        }
        catch (SqlException exception)
        {
            var (statusCode, message) = exception.Number switch
            {
                50500 => (StatusCodes.Status400BadRequest,
                    "Érvénytelen bérelem- vagy felhasználó-azonosító."),
                50501 => (StatusCodes.Status403Forbidden,
                    "A felhasználó nem található vagy inaktív."),
                50502 => (StatusCodes.Status404NotFound,
                    "A bérelem nem található."),
                _ => (StatusCodes.Status500InternalServerError,
                    "A dinamikus mezők lekérdezése sikertelen.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                loggerFactory.CreateLogger("PayrollElementFieldValueEndpoints")
                    .LogError(
                        exception,
                        "Hiba történt a bérelem dinamikus mezőinek lekérdezésekor. PayrollElementId: {PayrollElementId}",
                        payrollElementId);
            }

            return await ErrorAsync(
                auditLog,
                httpContext,
                "READ",
                payrollElementId.ToString(),
                message,
                statusCode,
                cancellationToken);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollElementFieldValueEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a bérelem dinamikus mezőinek lekérdezésekor. PayrollElementId: {PayrollElementId}",
                    payrollElementId);

            return await ErrorAsync(
                auditLog,
                httpContext,
                "READ",
                payrollElementId.ToString(),
                "A dinamikus mezők lekérdezése sikertelen.",
                StatusCodes.Status500InternalServerError,
                cancellationToken);
        }
    }

    private static async Task<IResult> SaveAsync(
        int payrollElementId,
        SavePayrollElementFieldValueRequest request,
        HttpContext httpContext,
        PayrollElementMaintenanceRepository payrollElements,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(httpContext);
        var objectId = $"{payrollElementId}/{request.FieldId}";
        var updatedValue =
            $"FieldValue={request.FieldValue}; OptionId={request.OptionId}; " +
            $"ValidFrom={request.ValidFrom:yyyy-MM-dd}; " +
            $"ValidTo={request.ValidTo?.ToString("yyyy-MM-dd") ?? "NULL"}";

        if (userId is null)
        {
            await WriteAuditAsync(
                auditLog, httpContext, "SAVE", objectId, updatedValue, false,
                "A bejelentkezett felhasználó azonosítója hiányzik.",
                cancellationToken);

            return Results.Unauthorized();
        }

        if (payrollElementId <= 0 || request.FieldId <= 0)
        {
            return await ErrorAsync(
                auditLog, httpContext, "SAVE", objectId,
                "Érvénytelen bérelem- vagy mezőazonosító.",
                StatusCodes.Status400BadRequest,
                cancellationToken,
                updatedValue);
        }

        if (request.ValidTo.HasValue &&
            request.ValidTo.Value < request.ValidFrom)
        {
            return await ErrorAsync(
                auditLog, httpContext, "SAVE", objectId,
                "Az érvényesség vége nem lehet korábbi a kezdeténél.",
                StatusCodes.Status400BadRequest,
                cancellationToken,
                updatedValue);
        }

        try
        {
            var result = await payrollElements.SaveFieldValueAsync(
                payrollElementId,
                userId.Value,
                request,
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                "SAVE",
                objectId,
                updatedValue,
                true,
                "A bérelem dinamikus mezőértékének mentése sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                result.PayrollElementId,
                result.FieldId,
                result.FieldValue,
                result.OptionId,
                result.ValidFrom,
                result.ValidTo,
                message = "A bérelem dinamikus mezőértékének mentése sikerült."
            });
        }
        catch (SqlException exception)
        {
            var (statusCode, message) = exception.Number switch
            {
                50520 or 50521 or 50526 or 50527 or 50528 or
                50529 or 50530 or 50531 or 50532 or 50533 =>
                    (StatusCodes.Status400BadRequest,
                     "A megadott mezőérték vagy érvényességi időszak érvénytelen."),
                50522 or 50525 =>
                    (StatusCodes.Status403Forbidden,
                     "A felhasználó inaktív, vagy nincs szerkesztési joga ehhez a mezőcsoporthoz."),
                50523 or 50524 =>
                    (StatusCodes.Status404NotFound,
                     "Az aktív bérelem vagy mező nem található."),
                50534 =>
                    (StatusCodes.Status409Conflict,
                     "Ez az egyedi mezőérték másik bérelemnél már érvényes ebben az időszakban."),
                _ =>
                    (StatusCodes.Status500InternalServerError,
                     "A bérelem dinamikus mezőértékének mentése sikertelen.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                loggerFactory.CreateLogger("PayrollElementFieldValueEndpoints")
                    .LogError(
                        exception,
                        "Hiba történt a bérelem mezőértékének mentésekor. PayrollElementId: {PayrollElementId}, FieldId: {FieldId}",
                        payrollElementId,
                        request.FieldId);
            }

            return await ErrorAsync(
                auditLog,
                httpContext,
                "SAVE",
                objectId,
                message,
                statusCode,
                cancellationToken,
                updatedValue);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollElementFieldValueEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a bérelem mezőértékének mentésekor. PayrollElementId: {PayrollElementId}, FieldId: {FieldId}",
                    payrollElementId,
                    request.FieldId);

            return await ErrorAsync(
                auditLog,
                httpContext,
                "SAVE",
                objectId,
                "A bérelem dinamikus mezőértékének mentése sikertelen.",
                StatusCodes.Status500InternalServerError,
                cancellationToken,
                updatedValue);
        }
    }

    private static int? GetUserId(HttpContext httpContext)
    {
        var value = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : null;
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string operation,
        string? objectId,
        string? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: operation,
            objectType: "PayrollElementFieldValue",
            objectId: objectId,
            originalValue: null,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken);
    }

    private static async Task<IResult> ErrorAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string operation,
        string? objectId,
        string message,
        int statusCode,
        CancellationToken cancellationToken,
        string? updatedValue = null)
    {
        await WriteAuditAsync(
            auditLog,
            httpContext,
            operation,
            objectId,
            updatedValue,
            false,
            message,
            cancellationToken);

        return Results.Json(new { message }, statusCode: statusCode);
    }
}