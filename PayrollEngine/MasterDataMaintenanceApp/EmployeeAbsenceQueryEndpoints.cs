using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeAbsenceQueryEndpoints
{
    public static WebApplication MapEmployeeAbsenceQueryEndpoints(
        this WebApplication app)
    {
        app.MapGet(
                "/api/employees/{employeeId:int}/absences",
                GetByEmployeeIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("GetEmployeeAbsences");

        return app;
    }

    private static async Task<IResult> GetByEmployeeIdAsync(
        int employeeId,
        EmployeeAbsenceRepository absences,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmployeeAbsenceQueryEndpoints");

        if (employeeId <= 0)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var result = await absences.GetByEmployeeIdAsync(
                employeeId,
                cancellationToken);

            await WriteAuditAsync(
                auditLog, httpContext, employeeId,
                success: true,
                "A munkavállaló távolléteinek lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50414)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50401)
        {
            const string message =
                "A munkavállaló nem található, vagy nincs belépési dátuma.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId,
                success: false, message, cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távollétek lekérdezésekor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message =
                "A munkavállaló távolléteinek lekérdezése sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId,
                success: false, message, cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: "READ_EMPLOYEE_ABSENCES",
            objectType: "EmployeeAbsence",
            objectId: employeeId.ToString(),
            originalValue: null,
            updatedValue: null,
            success: success,
            message: message,
            cancellationToken);
    }
}