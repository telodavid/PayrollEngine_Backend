using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.WorkScheduleConfigApp;

public static class EmployeeWorkScheduleChangeEndpoints
{
    public static WebApplication MapEmployeeWorkScheduleChangeEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/employees/{employeeId:int}/work-schedule",
                ChangeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
            .WithName("ChangeEmployeeWorkSchedule");
        app.MapGet(
        "/api/employees/{employeeId:int}/work-schedules",
        ListAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
    .WithName("ListEmployeeWorkSchedules");

        app.MapDelete(
        "/api/employees/{employeeId:int}/work-schedules/{employmentWorkScheduleId:int}",
        DeleteAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "WORKSCHEDULE_CONFIG"))
    .WithName("DeleteEmployeeWorkSchedule");

        return app;
    }

    private static async Task<IResult> DeleteAsync(
    int employeeId,
    int employmentWorkScheduleId,
    EmployeeWorkScheduleChangeRepository changes,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmployeeWorkScheduleChangeEndpoints");

        EmployeeWorkScheduleAssignmentListItem? existingItem = null;

        Task WriteDeleteAuditAsync(bool success, string message)
        {
            var originalValue = existingItem is null
                ? null
                : $"WorkScheduleId={existingItem.WorkScheduleId}; " +
                  $"WorkScheduleName={existingItem.WorkScheduleName}; " +
                  $"ValidFrom={existingItem.ValidFrom:yyyy-MM-dd}; " +
                  $"ValidTo={existingItem.ValidTo?.ToString("yyyy-MM-dd") ?? "NULL"}; " +
                  $"CycleStartDate={existingItem.CycleStartDate:yyyy-MM-dd}";

            return auditLog.WriteAsync(
                httpContext,
                operation: "DELETE_EMPLOYEE_WORK_SCHEDULE",
                objectType: "EmploymentWorkSchedule",
                objectId: employmentWorkScheduleId.ToString(),
                originalValue: originalValue,
                updatedValue: null,
                success: success,
                message: message,
                cancellationToken: cancellationToken);
        }

        if (employeeId <= 0 || employmentWorkScheduleId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy munkarend-hozzárendelés-azonosító.";

            await WriteDeleteAuditAsync(false, message);
            return Results.BadRequest(new { message });
        }

        try
        {
            var assignments = await changes.ListAsync(
                employeeId,
                cancellationToken);

            existingItem = assignments.FirstOrDefault(
                item => item.EmploymentWorkScheduleId ==
                        employmentWorkScheduleId);

            if (existingItem is null)
            {
                const string message =
                    "A munkarend-hozzárendelés nem található ennél a dolgozónál.";

                await WriteDeleteAuditAsync(false, message);
                return Results.NotFound(new { message });
            }

            await changes.DeleteAsync(
                employeeId,
                employmentWorkScheduleId,
                cancellationToken);

            const string successMessage =
                "A munkarend-hozzárendelés törlése sikerült.";

            await WriteDeleteAuditAsync(true, successMessage);

            return Results.Ok(new
            {
                employeeId,
                employmentWorkScheduleId,
                message = successMessage
            });
        }
        catch (SqlException exception) when (exception.Number == 50901)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy munkarend-hozzárendelés-azonosító.";

            await WriteDeleteAuditAsync(false, message);
            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50902)
        {
            const string message =
                "A munkarend-hozzárendelés nem található ennél a dolgozónál.";

            await WriteDeleteAuditAsync(false, message);
            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkarend-hozzárendelés törlésekor. EmployeeId: {EmployeeId}, AssignmentId: {AssignmentId}",
                employeeId,
                employmentWorkScheduleId);

            const string message =
                "A munkarend-hozzárendelés törlése sikertelen.";

            await WriteDeleteAuditAsync(false, message);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ListAsync(
    int employeeId,
    EmployeeWorkScheduleChangeRepository changes,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen munkavállaló-azonosító."
            });
        }

        try
        {
            var result = await changes.ListAsync(
                employeeId,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger(
                "EmployeeWorkScheduleChangeEndpoints");

            logger.LogError(
                exception,
                "Hiba történt a munkarendek lekérdezésekor. EmployeeId: {EmployeeId}",
                employeeId);

            return Results.Problem(
                title: "A munkarendek lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ChangeAsync(
        int employeeId,
        ChangeEmployeeWorkScheduleRequest request,
        EmployeeWorkScheduleChangeRepository changes,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmployeeWorkScheduleChangeEndpoints");

        if (employeeId <= 0 || request.WorkScheduleId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy munkarend-azonosító.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (request.CycleStartDate > request.ValidFrom)
        {
            const string message =
                "A ciklus kezdődátuma nem lehet későbbi a munkarend érvényességének kezdeténél.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var result = await changes.ChangeAsync(
                employeeId,
                request,
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request,
                success: !result.RequiresRetroPayroll,
                result.Message,
                cancellationToken);

            if (result.RequiresRetroPayroll)
            {
                return Results.Conflict(new
                {
                    requiresRetroPayroll = true,
                    suggestedPayrollRunFrom = result.SuggestedPayrollRunFrom,
                    message = result.Message
                });
            }

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50110)
        {
            return await SqlErrorAsync(
                StatusCodes.Status404NotFound,
                "A munkavállaló nem található, vagy nincs belépési dátuma.");
        }
        catch (SqlException exception) when (exception.Number is 50111 or 50112 or 50118)
        {
            return await SqlErrorAsync(
                StatusCodes.Status400BadRequest,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number is 50113 or 50114)
        {
            return await SqlErrorAsync(
                StatusCodes.Status400BadRequest,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number == 50115)
        {
            return await SqlErrorAsync(
                StatusCodes.Status409Conflict,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number is 50116 or 50117 or 50119 or 50120)
        {
            return await SqlErrorAsync(
                StatusCodes.Status409Conflict,
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkarend módosításakor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message = "A munkarend módosítása sikertelen.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request,
                success: false,
                message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }

        async Task<IResult> SqlErrorAsync(int statusCode, string message)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request,
                success: false,
                message,
                cancellationToken);

            return Results.Json(new { message }, statusCode: statusCode);
        }
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        ChangeEmployeeWorkScheduleRequest request,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: "CHANGE_WORK_SCHEDULE",
            objectType: "EmployeeWorkSchedule",
            objectId: employeeId.ToString(),
            originalValue: null,
            updatedValue:
                $"WorkScheduleId={request.WorkScheduleId}; " +
                $"ValidFrom={request.ValidFrom:yyyy-MM-dd}; " +
                $"CycleStartDate={request.CycleStartDate:yyyy-MM-dd}; " +
                $"RetroPayrollRunFrom={request.RetroPayrollRunFrom?.ToString("yyyy-MM-dd") ?? "null"}",
            success: success,
            message: message,
            cancellationToken);
    }
}