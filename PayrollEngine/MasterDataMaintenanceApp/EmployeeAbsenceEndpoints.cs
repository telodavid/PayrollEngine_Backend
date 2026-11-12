using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeAbsenceEndpoints
{
    public static WebApplication MapEmployeeAbsenceEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/employees/{employeeId:int}/absences",
                CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("CreateEmployeeAbsence");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        int employeeId,
        CreateEmployeeAbsenceRequest request,
        EmployeeAbsenceRepository absences,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("EmployeeAbsenceEndpoints");

        if (employeeId <= 0 || request.AbsenceTypeId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy távolléttípus-azonosító.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (request.StartDate == default || request.EndDate == default)
        {
            const string message =
                "A távollét kezdő és záró dátumát is meg kell adni.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (request.EndDate < request.StartDate)
        {
            const string message =
                "A távollét vége nem lehet korábbi a kezdeténél.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (request.FullDay &&
            (request.StartTime.HasValue || request.EndTime.HasValue))
        {
            const string message =
                "Egész napos távollétnél kezdő és záró időpont nem adható meg.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (!request.FullDay &&
            (!request.StartTime.HasValue || !request.EndTime.HasValue))
        {
            const string message =
                "Résznapos távollétnél a kezdő és záró időpont megadása kötelező.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (!request.FullDay &&
            request.StartDate == request.EndDate &&
            request.EndTime <= request.StartTime)
        {
            const string message =
                "A záró időpontnak későbbinek kell lennie a kezdő időpontnál.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var createdAbsences = await absences.CreateAsync(
                employeeId,
                request,
                cancellationToken);

            var absenceIds = string.Join(
                ",",
                createdAbsences.Select(absence => absence.AbsenceId));

            const string successMessage = "A távollét rögzítése sikerült.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, absenceIds, request,
                success: true, successMessage, cancellationToken);

            return Results.Ok(new
            {
                employeeId,
                absences = createdAbsences,
                message = successMessage
            });
        }
        catch (SqlException exception) when (exception.Number == 50401)
        {
            return await SqlErrorAsync(
                StatusCodes.Status404NotFound,
                "A munkavállaló nem található, vagy nincs belépési dátuma.");
        }
        catch (SqlException exception) when (exception.Number == 50402)
        {
            return await SqlErrorAsync(
                StatusCodes.Status400BadRequest,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number is 50403 or 50407)
        {
            return await SqlErrorAsync(
                StatusCodes.Status400BadRequest,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number == 50404)
        {
            return await SqlErrorAsync(
                StatusCodes.Status404NotFound,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number is 50405 or 50406)
        {
            return await SqlErrorAsync(
                StatusCodes.Status409Conflict,
                exception.Message);
        }
        catch (SqlException exception)
            when (exception.Number is >= 50410 and <= 50413)
        {
            return await SqlErrorAsync(
                StatusCodes.Status400BadRequest,
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távollét rögzítésekor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message = "A távollét rögzítése sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }

        async Task<IResult> SqlErrorAsync(int statusCode, string message)
        {
            await WriteAuditAsync(
                auditLog, httpContext, employeeId, null, request,
                success: false, message, cancellationToken);

            return Results.Json(new { message }, statusCode: statusCode);
        }
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        string? absenceIds,
        CreateEmployeeAbsenceRequest request,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        var startTime = request.StartTime?.ToString("HH:mm:ss.fffffff") ?? "null";
        var endTime = request.EndTime?.ToString("HH:mm:ss.fffffff") ?? "null";

        return auditLog.WriteAsync(
            httpContext,
            operation: "CREATE_EMPLOYEE_ABSENCE",
            objectType: "EmployeeAbsence",
            objectId: absenceIds,
            originalValue: null,
            updatedValue:
                $"EmployeeId={employeeId}; " +
                $"AbsenceTypeId={request.AbsenceTypeId}; " +
                $"StartDate={request.StartDate:yyyy-MM-dd}; " +
                $"EndDate={request.EndDate:yyyy-MM-dd}; " +
                $"FullDay={request.FullDay}; " +
                $"StartTime={startTime}; EndTime={endTime}",
            success: success,
            message: message,
            cancellationToken);
    }
}