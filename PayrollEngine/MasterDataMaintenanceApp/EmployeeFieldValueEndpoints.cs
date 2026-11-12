using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;
using Microsoft.AspNetCore.Mvc;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeFieldValueEndpoints
{
    public static WebApplication MapEmployeeFieldValueEndpoints(
        this WebApplication app)
    {
        app.MapGet(
                "/api/employees/{employeeId:int}/field-values",
                GetByEmployeeIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("GetEmployeeFieldValues");

        app.MapPut(
                "/api/employees/{employeeId:int}/field-values/{fieldId:int}",
                SaveAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("SaveEmployeeFieldValue");

        app.MapDelete(
                "/api/employees/{employeeId:int}/field-values/{fieldId:int}",
                EndAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("EndEmployeeFieldValue");

        return app;
    }

    private static async Task<IResult> GetByEmployeeIdAsync(
        int employeeId,
        DateOnly? asOfDate,
        HttpContext httpContext,
        EmployeeFieldValueRepository fieldValues,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(httpContext);

        if (userId is null)
        {
            await WriteAuditAsync(
                httpContext, auditLog, "READ", employeeId.ToString(),
                null, null, false,
                "A bejelentkezett felhasználó azonosítója hiányzik.",
                cancellationToken);

            return Results.Unauthorized();
        }

        if (employeeId <= 0)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteAuditAsync(
                httpContext, auditLog, "READ", employeeId.ToString(),
                null, null, false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var groups = await fieldValues.GetByEmployeeIdAsync(
                employeeId,
                userId.Value,
                asOfDate,
                cancellationToken);

            var fieldCount = groups.Sum(group => group.Fields.Count);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                "READ",
                employeeId.ToString(),
                null,
                $"GroupCount={groups.Count}; FieldCount={fieldCount}; AsOfDate={asOfDate?.ToString("yyyy-MM-dd") ?? "CURRENT"}",
                true,
                "A munkavállalói egyedi mezők lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(groups);
        }
        catch (SqlException exception)
        {
            return await HandleSqlExceptionAsync(
                exception,
                "READ",
                employeeId.ToString(),
                "A munkavállalói egyedi mezők lekérdezése sikertelen.",
                httpContext,
                auditLog,
                loggerFactory,
                cancellationToken);
        }
        catch (Exception exception)
        {
            loggerFactory
                .CreateLogger("EmployeeFieldValueEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a munkavállalói egyedi mezők lekérdezésekor. EmployeeId: {EmployeeId}",
                    employeeId);

            await WriteAuditAsync(
                httpContext, auditLog, "READ", employeeId.ToString(),
                null, null, false,
                "A munkavállalói egyedi mezők lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A munkavállalói egyedi mezők lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> SaveAsync(
        int employeeId,
        int fieldId,
        SaveEmployeeFieldValueRequest request,
        HttpContext httpContext,
        EmployeeFieldValueRepository fieldValues,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(httpContext);
        var objectId = $"{employeeId}/{fieldId}";

        if (userId is null)
        {
            await WriteAuditAsync(
                httpContext, auditLog, "SAVE", objectId,
                null, null, false,
                "A bejelentkezett felhasználó azonosítója hiányzik.",
                cancellationToken);

            return Results.Unauthorized();
        }

        if (employeeId <= 0 || fieldId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy mezőazonosító.";

            await WriteAuditAsync(
                httpContext, auditLog, "SAVE", objectId,
                null, null, false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        var hasFieldValue = request.FieldValue is not null;
        var hasOptionId = request.OptionId.HasValue;

        if (hasFieldValue == hasOptionId)
        {
            const string message =
                "Pontosan egy mezőértéket vagy listaértéket kell megadni.";

            await WriteAuditAsync(
                httpContext, auditLog, "SAVE", objectId,
                null, null, false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        string? originalValue = null;

        try
        {
            originalValue = await ReadCurrentFieldValueAsync(
                fieldValues,
                employeeId,
                fieldId,
                userId.Value,
                request.ValidFrom,
                cancellationToken);

            var result = await fieldValues.SaveAsync(
                employeeId,
                fieldId,
                userId.Value,
                request,
                cancellationToken);

            var updatedValue = JsonSerializer.Serialize(new
            {
                request.FieldValue,
                request.OptionId,
                result.ValidFrom,
                result.ValidTo
            });

            await WriteAuditAsync(
                httpContext,
                auditLog,
                "SAVE",
                objectId,
                originalValue,
                updatedValue,
                true,
                "A munkavállalói mezőérték mentése sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                result.EmployeeId,
                result.FieldId,
                result.ValidFrom,
                result.ValidTo,
                result.RequiresRetroPayroll,
                result.SuggestedPayrollRunFrom,
                message = "A munkavállalói mezőérték mentése sikerült."
            });
        }
        catch (SqlException exception)
        {
            return await HandleSqlExceptionAsync(
                exception,
                "SAVE",
                objectId,
                "A munkavállalói mezőérték mentése sikertelen.",
                httpContext,
                auditLog,
                loggerFactory,
                cancellationToken,
                originalValue);
        }
        catch (Exception exception)
        {
            loggerFactory
                .CreateLogger("EmployeeFieldValueEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a munkavállalói mezőérték mentésekor. EmployeeId: {EmployeeId}, FieldId: {FieldId}",
                    employeeId,
                    fieldId);

            await WriteAuditAsync(
                httpContext, auditLog, "SAVE", objectId,
                originalValue, null, false,
                "A munkavállalói mezőérték mentése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A munkavállalói mezőérték mentése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> EndAsync(
        int employeeId,
        int fieldId,
        [FromBody] EndEmployeeFieldValueRequest request,
        HttpContext httpContext,
        EmployeeFieldValueRepository fieldValues,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(httpContext);
        var objectId = $"{employeeId}/{fieldId}";

        if (userId is null)
        {
            await WriteAuditAsync(
                httpContext, auditLog, "END", objectId,
                null, null, false,
                "A bejelentkezett felhasználó azonosítója hiányzik.",
                cancellationToken);

            return Results.Unauthorized();
        }

        if (employeeId <= 0 || fieldId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy mezőazonosító.";

            await WriteAuditAsync(
                httpContext, auditLog, "END", objectId,
                null, null, false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (request.ValidTo < request.ValidFrom)
        {
            const string message =
                "Az érvényesség vége nem lehet korábbi a kezdeténél.";

            await WriteAuditAsync(
                httpContext, auditLog, "END", objectId,
                null, null, false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        string? originalValue = null;

        try
        {
            originalValue = await ReadCurrentFieldValueAsync(
                fieldValues,
                employeeId,
                fieldId,
                userId.Value,
                request.ValidFrom,
                cancellationToken);

            var result = await fieldValues.EndAsync(
                employeeId,
                fieldId,
                userId.Value,
                request,
                cancellationToken);

            var updatedValue = JsonSerializer.Serialize(new
            {
                result.ValidFrom,
                result.ValidTo
            });

            await WriteAuditAsync(
                httpContext,
                auditLog,
                "END",
                objectId,
                originalValue,
                updatedValue,
                true,
                "A munkavállalói mezőérték érvényessége lezárult.",
                cancellationToken);

            return Results.Ok(new
            {
                result.EmployeeId,
                result.FieldId,
                result.ValidFrom,
                result.ValidTo,
                result.RequiresRetroPayroll,
                result.SuggestedPayrollRunFrom,
                message = "A mezőérték érvényességének lezárása sikerült."
            });
        }
        catch (SqlException exception)
        {
            return await HandleSqlExceptionAsync(
                exception,
                "END",
                objectId,
                "A mezőérték érvényességének lezárása sikertelen.",
                httpContext,
                auditLog,
                loggerFactory,
                cancellationToken,
                originalValue);
        }
        catch (Exception exception)
        {
            loggerFactory
                .CreateLogger("EmployeeFieldValueEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a mezőérték lezárásakor. EmployeeId: {EmployeeId}, FieldId: {FieldId}",
                    employeeId,
                    fieldId);

            await WriteAuditAsync(
                httpContext, auditLog, "END", objectId,
                originalValue, null, false,
                "A mezőérték érvényességének lezárása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A mezőérték érvényességének lezárása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<string?> ReadCurrentFieldValueAsync(
        EmployeeFieldValueRepository fieldValues,
        int employeeId,
        int fieldId,
        int userId,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        var groups = await fieldValues.GetByEmployeeIdAsync(
            employeeId,
            userId,
            asOfDate,
            cancellationToken);

        var field = groups
            .SelectMany(group => group.Fields)
            .FirstOrDefault(item => item.FieldId == fieldId);

        return field is null
            ? null
            : JsonSerializer.Serialize(new
            {
                field.FieldValue,
                field.OptionId,
                field.ValidFrom,
                field.ValidTo
            });
    }

    private static async Task<IResult> HandleSqlExceptionAsync(
        SqlException exception,
        string operation,
        string objectId,
        string failureTitle,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string? originalValue = null)
    {
        var (statusCode, message) = exception.Number switch
        {
            50300 or 50310 =>
                (StatusCodes.Status400BadRequest,
                    "Érvénytelen munkavállaló-, mező- vagy felhasználó-azonosító."),
            50301 =>
                (StatusCodes.Status403Forbidden,
                    "A felhasználó nem található vagy inaktív."),
            50302 =>
                (StatusCodes.Status404NotFound,
                    "A munkavállaló nem található."),
            50303 =>
                (StatusCodes.Status400BadRequest,
                    "Az érték kezdődátuma nem lehet korábbi a belépés dátumánál."),
            50304 =>
                (StatusCodes.Status404NotFound,
                    "A dolgozói mező nem található vagy inaktív."),
            50305 =>
                (StatusCodes.Status403Forbidden,
                    "Nincs szerkesztési joga ehhez a törzsadatcsoporthoz."),
            50306 =>
                (StatusCodes.Status400BadRequest,
                    "A megadott mezőérték érvénytelen."),
            50307 =>
                (StatusCodes.Status400BadRequest,
                    "A listaérték nem található vagy inaktív."),
            50308 =>
                (StatusCodes.Status409Conflict,
                    "Ez az egyedi mezőérték másik munkavállalónál már érvényes ebben az időszakban."),
            50309 =>
                (StatusCodes.Status400BadRequest,
                    "Érvénytelen visszamenőleges számfejtési kezdődátum."),
            50311 =>
                (StatusCodes.Status400BadRequest,
                    "Érvénytelen vagy másik mezőérték időszakával ütköző érvényességi időszak."),
            50312 =>
                (StatusCodes.Status400BadRequest,
                    "Az érvényesség vége nem lehet korábbi a belépés dátumánál."),
            50313 =>
                (StatusCodes.Status404NotFound,
                    "A megadott mezőérték nem található."),
            50314 =>
                (StatusCodes.Status409Conflict,
                    "Ezzel a művelettel csak rövidíteni lehet az érvényességi időszakot."),
            2601 or 2627 =>
                (StatusCodes.Status409Conflict,
                    "A mezőérték ütközik egy már rögzített értékkel."),
            _ =>
                (StatusCodes.Status500InternalServerError, failureTitle)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            loggerFactory
                .CreateLogger("EmployeeFieldValueEndpoints")
                .LogError(
                    exception,
                    "Adatbázishiba a munkavállalói mezőérték műveleténél. ObjectId: {ObjectId}",
                    objectId);
        }

        await WriteAuditAsync(
            httpContext,
            auditLog,
            operation,
            objectId,
            originalValue,
            null,
            false,
            message,
            cancellationToken);

        return statusCode == StatusCodes.Status500InternalServerError
            ? Results.Problem(title: message, statusCode: statusCode)
            : Results.Json(new { message }, statusCode: statusCode);
    }

    private static int? GetUserId(HttpContext httpContext)
    {
        var userIdValue = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdValue, out var userId) && userId > 0
            ? userId
            : null;
    }

    private static Task WriteAuditAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        string operation,
        string? objectId,
        string? originalValue,
        string? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: operation,
            objectType: "EmployeeFieldValue",
            objectId: objectId,
            originalValue: originalValue,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken);
    }
}