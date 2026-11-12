using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.WorkScheduleConfigApp;

public static class WorkScheduleEndpoints
{
    public static WebApplication MapWorkScheduleEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/work-schedules", SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("SearchWorkSchedules");

        app.MapGet("/api/work-schedules/{workScheduleId:int}", GetByIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("GetWorkSchedule");

        app.MapPost("/api/work-schedules", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("CreateWorkSchedule");

        app.MapPut("/api/work-schedules/{workScheduleId:int}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("UpdateWorkSchedule");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        bool? isActive,
        WorkScheduleRepository schedules,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("WorkScheduleEndpoints");

        try
        {
            var result = await schedules.SearchAsync(
                search,
                isActive,
                cancellationToken);

            return await AuditedAsync(
                Results.Ok(result),
                httpContext,
                auditLog,
                logger,
                "READ_LIST",
                null,
                null,
                new
                {
                    search,
                    isActive,
                    resultCount = result.Count
                },
                true,
                "A munkarendek lekérdezése sikerült.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkarendek lekérdezésekor.");

            return await AuditedAsync(
                Results.Problem(
                    title: "A munkarendek lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                "READ_LIST",
                null,
                null,
                new { search, isActive },
                false,
                "A munkarendek lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> GetByIdAsync(
        int workScheduleId,
        WorkScheduleRepository schedules,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("WorkScheduleEndpoints");

        if (workScheduleId <= 0)
        {
            return await AuditedAsync(
                Results.BadRequest(new
                {
                    message = "Érvénytelen munkarend-azonosító."
                }),
                httpContext,
                auditLog,
                logger,
                "READ",
                workScheduleId.ToString(),
                null,
                null,
                false,
                "Érvénytelen munkarend-azonosító.",
                cancellationToken);
        }

        try
        {
            var result = await schedules.GetByIdAsync(
                workScheduleId,
                cancellationToken);

            if (result is null)
            {
                return await AuditedAsync(
                    Results.NotFound(new
                    {
                        message = "A munkarend nem található."
                    }),
                    httpContext,
                    auditLog,
                    logger,
                    "READ",
                    workScheduleId.ToString(),
                    null,
                    null,
                    false,
                    "A munkarend nem található.",
                    cancellationToken);
            }

            return await AuditedAsync(
                Results.Ok(result),
                httpContext,
                auditLog,
                logger,
                "READ",
                workScheduleId.ToString(),
                null,
                result,
                true,
                "A munkarend lekérdezése sikerült.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50072)
        {
            return await AuditedAsync(
                Results.NotFound(new
                {
                    message = "A munkarend nem található."
                }),
                httpContext,
                auditLog,
                logger,
                "READ",
                workScheduleId.ToString(),
                null,
                null,
                false,
                "A munkarend nem található.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50071)
        {
            return await AuditedAsync(
                Results.BadRequest(new
                {
                    message = "Érvénytelen munkarend-azonosító."
                }),
                httpContext,
                auditLog,
                logger,
                "READ",
                workScheduleId.ToString(),
                null,
                null,
                false,
                "Érvénytelen munkarend-azonosító.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkarend lekérdezésekor. WorkScheduleId: {WorkScheduleId}",
                workScheduleId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A munkarend lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                "READ",
                workScheduleId.ToString(),
                null,
                null,
                false,
                "A munkarend lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkScheduleRequest request,
        WorkScheduleRepository schedules,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("WorkScheduleEndpoints");

        var validationError = ValidateSchedule(
            request.Name,
            request.PeriodLengthDays,
            request.Days);

        if (validationError is not null)
        {
            return await AuditedAsync(
                Results.BadRequest(new { message = validationError }),
                httpContext,
                auditLog,
                logger,
                "CREATE",
                null,
                null,
                request,
                false,
                validationError,
                cancellationToken);
        }

        try
        {
            var workScheduleId = await schedules.CreateAsync(
                request,
                cancellationToken);

            var response = new
            {
                workScheduleId,
                request.Name,
                request.PeriodLengthDays,
                isActive = true,
                days = request.Days,
                message = "A munkarend létrejött."
            };

            return await AuditedAsync(
                Results.Created(
                    $"/api/work-schedules/{workScheduleId}",
                    response),
                httpContext,
                auditLog,
                logger,
                "CREATE",
                workScheduleId.ToString(),
                null,
                request,
                true,
                "A munkarend létrejött.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is >= 50060 and <= 50063)
        {
            const string errorMessage =
                "A munkarend adatai érvénytelenek, vagy egy hivatkozott naptípus nem használható.";

            return await AuditedAsync(
                Results.BadRequest(new { message = errorMessage }),
                httpContext,
                auditLog,
                logger,
                "CREATE",
                null,
                null,
                request,
                false,
                errorMessage,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkarend létrehozásakor. Name: {Name}",
                request.Name);

            return await AuditedAsync(
                Results.Problem(
                    title: "A munkarend létrehozása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                "CREATE",
                null,
                null,
                request,
                false,
                "A munkarend létrehozása sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int workScheduleId,
        UpdateWorkScheduleRequest request,
        WorkScheduleRepository schedules,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        AuditLogService auditLog,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("WorkScheduleEndpoints");

        if (workScheduleId <= 0)
        {
            return await AuditedAsync(
                Results.BadRequest(new
                {
                    message = "Érvénytelen munkarend-azonosító."
                }),
                httpContext,
                auditLog,
                logger,
                "UPDATE",
                workScheduleId.ToString(),
                null,
                request,
                false,
                "Érvénytelen munkarend-azonosító.",
                cancellationToken);
        }

        var validationError = ValidateSchedule(
            request.Name,
            request.PeriodLengthDays,
            request.Days);

        if (validationError is not null)
        {
            return await AuditedAsync(
                Results.BadRequest(new { message = validationError }),
                httpContext,
                auditLog,
                logger,
                "UPDATE",
                workScheduleId.ToString(),
                null,
                request,
                false,
                validationError,
                cancellationToken);
        }

        WorkScheduleDetails? original = null;

        try
        {
            original = await schedules.GetByIdAsync(
                workScheduleId,
                cancellationToken);

            if (original is null)
            {
                return await AuditedAsync(
                    Results.NotFound(new
                    {
                        message = "A munkarend nem található."
                    }),
                    httpContext,
                    auditLog,
                    logger,
                    "UPDATE",
                    workScheduleId.ToString(),
                    null,
                    request,
                    false,
                    "A munkarend nem található.",
                    cancellationToken);
            }

            await schedules.UpdateAsync(
                workScheduleId,
                request,
                cancellationToken);

            return await AuditedAsync(
                Results.Ok(new
                {
                    workScheduleId,
                    request.Name,
                    request.PeriodLengthDays,
                    request.IsActive,
                    days = request.Days,
                    message = "A munkarend adatai módosultak."
                }),
                httpContext,
                auditLog,
                logger,
                "UPDATE",
                workScheduleId.ToString(),
                original,
                request,
                true,
                "A munkarend adatai módosultak.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50072)
        {
            return await AuditedAsync(
                Results.NotFound(new
                {
                    message = "A munkarend nem található."
                }),
                httpContext,
                auditLog,
                logger,
                "UPDATE",
                workScheduleId.ToString(),
                original,
                request,
                false,
                "A munkarend nem található.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is >= 50064 and <= 50070)
        {
            const string errorMessage =
                "A munkarend adatai érvénytelenek, vagy egy hivatkozott naptípus nem használható.";

            return await AuditedAsync(
                Results.BadRequest(new { message = errorMessage }),
                httpContext,
                auditLog,
                logger,
                "UPDATE",
                workScheduleId.ToString(),
                original,
                request,
                false,
                errorMessage,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkarend módosításakor. WorkScheduleId: {WorkScheduleId}",
                workScheduleId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A munkarend módosítása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                httpContext,
                auditLog,
                logger,
                "UPDATE",
                workScheduleId.ToString(),
                original,
                request,
                false,
                "A munkarend módosítása sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> AuditedAsync(
        IResult result,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILogger logger,
        string operation,
        string? objectId,
        object? originalValue,
        object? updatedValue,
        bool success,
        string? message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                operation,
                "WorkSchedule",
                objectId,
                SerializeAuditValue(originalValue),
                SerializeAuditValue(updatedValue),
                success,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Az auditbejegyzés fájlba írása sikertelen. " +
                "Művelet: {Operation}, azonosító: {ObjectId}",
                operation,
                objectId);
        }

        return result;
    }

    private static string? SerializeAuditValue(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }

    private static string? ValidateSchedule(
        string name,
        int periodLengthDays,
        IReadOnlyList<WorkScheduleDayRequest> days)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return "A munkarend neve nem lehet üres, és legfeljebb 100 karakter lehet.";
        }

        if (periodLengthDays <= 0)
        {
            return "A ciklus hossza legalább egy nap legyen.";
        }

        if (days is null || days.Count != periodLengthDays)
        {
            return "A ciklus minden napjához meg kell adni egy naptípust.";
        }

        if (days.Any(day =>
                day.DayOfPeriod < 1 ||
                day.DayOfPeriod > periodLengthDays ||
                day.DayTypeId <= 0))
        {
            return "A ciklusnapok és a naptípus-azonosítók értéke érvénytelen.";
        }

        if (days.Select(day => day.DayOfPeriod).Distinct().Count() !=
            periodLengthDays)
        {
            return "Minden ciklusnapot pontosan egyszer kell megadni.";
        }

        return null;
    }
}