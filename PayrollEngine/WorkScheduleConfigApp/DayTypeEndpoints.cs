using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.WorkScheduleConfigApp;

public static class DayTypeEndpoints
{
    public static WebApplication MapDayTypeEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/day-types", GetDayTypesAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("GetDayTypes");

        app.MapGet("/api/day-types/{dayTypeId:int}", GetDayTypeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("GetDayType");

        app.MapPost("/api/day-types", CreateDayTypeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("CreateDayType");

        app.MapPut("/api/day-types/{dayTypeId:int}", UpdateDayTypeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("UpdateDayType");

        app.MapDelete("/api/day-types/{dayTypeId:int}", DeleteDayTypeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("DeleteDayType");

        return app;
    }

    private static async Task<IResult> GetDayTypesAsync(
        string? search,
        bool? isWorkday,
        DayTypeRepository dayTypes,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DayTypeEndpoints");

        try
        {
            var result = await dayTypes.SearchAsync(
                search,
                isWorkday,
                cancellationToken);

            return await AuditedResultAsync(
                Results.Ok(result),
                httpContext,
                auditLog,
                logger,
                operation: "READ_LIST",
                objectId: null,
                originalValue: null,
                updatedValue: new
                {
                    search,
                    isWorkday,
                    resultCount = result.Count
                },
                success: true,
                message: "A naptípusok lekérdezése sikerült.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Hiba történt a naptípusok lekérdezésekor.");

            return await AuditedResultAsync(
                Results.Problem(
                    title: "A naptípusok lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                operation: "READ_LIST",
                objectId: null,
                originalValue: null,
                updatedValue: new { search, isWorkday },
                success: false,
                message: "A naptípusok lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> GetDayTypeAsync(
        int dayTypeId,
        DayTypeRepository dayTypes,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DayTypeEndpoints");

        if (dayTypeId <= 0)
        {
            return await AuditedResultAsync(
                Results.BadRequest(new
                {
                    message = "Érvénytelen naptípus-azonosító."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "READ",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "Érvénytelen naptípus-azonosító.",
                cancellationToken);
        }

        try
        {
            var result = await dayTypes.GetByIdAsync(
                dayTypeId,
                cancellationToken);

            if (result is null)
            {
                return await AuditedResultAsync(
                    Results.NotFound(new
                    {
                        message = "A naptípus nem található."
                    }),
                    httpContext,
                    auditLog,
                    logger,
                    operation: "READ",
                    objectId: dayTypeId.ToString(),
                    originalValue: null,
                    updatedValue: null,
                    success: false,
                    message: "A naptípus nem található.",
                    cancellationToken);
            }

            return await AuditedResultAsync(
                Results.Ok(result),
                httpContext,
                auditLog,
                logger,
                operation: "READ",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: result,
                success: true,
                message: "A naptípus lekérdezése sikerült.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50052)
        {
            return await AuditedResultAsync(
                Results.BadRequest(new
                {
                    message = "Érvénytelen naptípus-azonosító."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "READ",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "Érvénytelen naptípus-azonosító.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50053)
        {
            return await AuditedResultAsync(
                Results.NotFound(new
                {
                    message = "A naptípus nem található."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "READ",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A naptípus nem található.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a naptípus lekérdezésekor. DayTypeId: {DayTypeId}",
                dayTypeId);

            return await AuditedResultAsync(
                Results.Problem(
                    title: "A naptípus lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                operation: "READ",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A naptípus lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> CreateDayTypeAsync(
        DayTypeRequest request,
        DayTypeRepository dayTypes,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DayTypeEndpoints");
        var validationError = ValidateRequest(request);

        if (validationError is not null)
        {
            return await AuditedResultAsync(
                Results.BadRequest(new { message = validationError }),
                httpContext,
                auditLog,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: request,
                success: false,
                message: validationError,
                cancellationToken);
        }

        try
        {
            var dayTypeId = await dayTypes.CreateAsync(
                request,
                cancellationToken);

            var response = new
            {
                dayTypeId,
                request.Code,
                request.Name,
                request.IsWorkday,
                request.StartTime,
                request.EndTime,
                request.BreakMinutes,
                message = "A naptípus létrejött."
            };

            return await AuditedResultAsync(
                Results.Created($"/api/day-types/{dayTypeId}", response),
                httpContext,
                auditLog,
                logger,
                operation: "CREATE",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: request,
                success: true,
                message: "A naptípus létrejött.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            return await AuditedResultAsync(
                Results.Conflict(new
                {
                    message = "Ez a naptípuskód már létezik."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: request,
                success: false,
                message: "Ez a naptípuskód már létezik.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is >= 50040 and <= 50044)
        {
            return await AuditedResultAsync(
                Results.BadRequest(new
                {
                    message = "A megadott naptípus-adatok érvénytelenek."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: request,
                success: false,
                message : "A megadott naptípus-adatok érvénytelenek.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a naptípus létrehozásakor. Code: {Code}",
                request.Code);

            return await AuditedResultAsync(
                Results.Problem(
                    title: "A naptípus létrehozása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: request,
                success: false,
                message: "A naptípus létrehozása sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> UpdateDayTypeAsync(
        int dayTypeId,
        DayTypeRequest request,
        DayTypeRepository dayTypes,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DayTypeEndpoints");

        if (dayTypeId <= 0)
        {
            return await AuditedResultAsync(
                Results.BadRequest(new
                {
                    message = "Érvénytelen naptípus-azonosító."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "UPDATE",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: request,
                success: false,
                message: "Érvénytelen naptípus-azonosító.",
                cancellationToken);
        }

        var validationError = ValidateRequest(request);

        if (validationError is not null)
        {
            return await AuditedResultAsync(
                Results.BadRequest(new { message = validationError }),
                httpContext,
                auditLog,
                logger,
                operation: "UPDATE",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: request,
                success: false,
                message: validationError,
                cancellationToken);
        }

        try
        {
            var original = await dayTypes.GetByIdAsync(
                dayTypeId,
                cancellationToken);

            if (original is null)
            {
                return await AuditedResultAsync(
                    Results.NotFound(new
                    {
                        message = "A naptípus nem található."
                    }),
                    httpContext,
                    auditLog,
                    logger,
                    operation: "UPDATE",
                    objectId: dayTypeId.ToString(),
                    originalValue: null,
                    updatedValue: request,
                    success: false,
                    message: "A naptípus nem található.",
                    cancellationToken);
            }

            await dayTypes.UpdateAsync(
                dayTypeId,
                request,
                cancellationToken);

            return await AuditedResultAsync(
                Results.Ok(new
                {
                    dayTypeId,
                    request.Code,
                    request.Name,
                    request.IsWorkday,
                    request.StartTime,
                    request.EndTime,
                    request.BreakMinutes,
                    message = "A naptípus adatai módosultak."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "UPDATE",
                objectId: dayTypeId.ToString(),
                originalValue: original,
                updatedValue: request,
                success: true,
                message: "A naptípus adatai módosultak.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50053)
        {
            return await AuditedResultAsync(
                Results.NotFound(new
                {
                    message = "A naptípus nem található."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "UPDATE",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: request,
                success: false,
                message: "A naptípus nem található.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            return await AuditedResultAsync(
                Results.Conflict(new
                {
                    message = "Ez a naptípuskód már használatban van."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "UPDATE",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: request,
                success: false,
                message: "Ez a naptípuskód már használatban van.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a naptípus módosításakor. DayTypeId: {DayTypeId}",
                dayTypeId);

            return await AuditedResultAsync(
                Results.Problem(
                    title: "A naptípus módosítása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                operation: "UPDATE",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: request,
                success: false,
                message: "A naptípus módosítása sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> DeleteDayTypeAsync(
        int dayTypeId,
        DayTypeRepository dayTypes,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DayTypeEndpoints");

        if (dayTypeId <= 0)
        {
            return await AuditedResultAsync(
                Results.BadRequest(new
                {
                    message = "Érvénytelen naptípus-azonosító."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "DELETE",
                objectId: dayTypeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "Érvénytelen naptípus-azonosító.",
                cancellationToken);
        }

        DayTypeModel? original = null;

        try
        {
            original = await dayTypes.GetByIdAsync(
                dayTypeId,
                cancellationToken);

            await dayTypes.DeleteAsync(dayTypeId, cancellationToken);

            return await AuditedResultAsync(
                Results.Ok(new
                {
                    dayTypeId,
                    message = "A naptípus törlése sikerült."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "DELETE",
                objectId: dayTypeId.ToString(),
                originalValue: original,
                updatedValue: null,
                success: true,
                message: "A naptípus törlése sikerült.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50053)
        {
            return await AuditedResultAsync(
                Results.NotFound(new
                {
                    message = "A naptípus nem található."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "DELETE",
                objectId: dayTypeId.ToString(),
                originalValue: original,
                updatedValue: null,
                success: false,
                message: "A naptípus nem található.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50055)
        {
            return await AuditedResultAsync(
                Results.NotFound(new
                {
                    message = "A naptípus nem található."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "DELETE",
                objectId: dayTypeId.ToString(),
                originalValue: original,
                updatedValue: null,
                success: false,
                message: "A naptípus nem található.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50056)
        {
            return await AuditedResultAsync(
                Results.Conflict(new
                {
                    message = "A naptípus munkarendben szerepel, ezért nem törölhető."
                }),
                httpContext,
                auditLog,
                logger,
                operation: "DELETE",
                objectId: dayTypeId.ToString(),
                originalValue: original,
                updatedValue: null,
                success: false,
                message : "A naptípus munkarendben szerepel, ezért nem törölhető.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a naptípus törlésekor. DayTypeId: {DayTypeId}",
                dayTypeId);

            return await AuditedResultAsync(
                Results.Problem(
                    title: "A naptípus törlése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                operation: "DELETE",
                objectId: dayTypeId.ToString(),
                originalValue: original,
                updatedValue: null,
                success: false,
                message: "A naptípus törlése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> AuditedResultAsync(
        IResult result,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILogger logger,
        string operation,
        string? objectId,
        object? originalValue,
        object? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: operation,
                objectType: "DayType",
                objectId: objectId,
                originalValue: SerializeAuditValue(originalValue),
                updatedValue: SerializeAuditValue(updatedValue),
                success: success,
                message: message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Az auditbejegyzés írása sikertelen. Operation: {Operation}, ObjectId: {ObjectId}",
                operation,
                objectId);
        }

        return result;
    }

    private static string? SerializeAuditValue(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }

    private static string? ValidateRequest(DayTypeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) ||
            request.Code.Length > 30)
        {
            return "A naptípuskód nem lehet üres, és legfeljebb 30 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length > 100)
        {
            return "A naptípus neve nem lehet üres, és legfeljebb 100 karakter lehet.";
        }

        if (request.BreakMinutes < 0)
        {
            return "A szünet hossza nem lehet negatív.";
        }

        return null;
    }
}