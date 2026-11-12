using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeAbsenceDeleteEndpoints
{
    public static WebApplication MapEmployeeAbsenceDeleteEndpoints(
        this WebApplication app)
    {
        app.MapDelete(
                "/api/employees/{employeeId:int}/absences/{absenceId:int}",
                DeleteAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("DeleteEmployeeAbsence");

        return app;
    }

    private static async Task<IResult> DeleteAsync(
        int employeeId,
        int absenceId,
        EmployeeAbsenceRepository absences,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmployeeAbsenceDeleteEndpoints");

        if (employeeId <= 0 || absenceId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy távollét-azonosító.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                absenceId,
                deletedAbsence: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var deletedAbsence = await absences.DeleteAsync(
                employeeId,
                absenceId,
                cancellationToken);

            const string successMessage = "A távollét törlése sikerült.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                absenceId,
                deletedAbsence,
                success: true,
                successMessage,
                cancellationToken);

            return Results.Ok(new
            {
                employeeId,
                deletedAbsence,
                message = successMessage
            });
        }
        catch (SqlException exception) when (exception.Number is 50408 or 50414)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy távollét-azonosító.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                absenceId,
                deletedAbsence: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50409)
        {
            const string message =
                "A távollét nem található ennél a munkavállalónál.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                absenceId,
                deletedAbsence: null,
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a távollét törlésekor. EmployeeId: {EmployeeId}, AbsenceId: {AbsenceId}",
                employeeId,
                absenceId);

            const string message = "A távollét törlése sikertelen.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                absenceId,
                deletedAbsence: null,
                success: false,
                message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        int absenceId,
        EmployeeAbsenceRecord? deletedAbsence,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        var originalValue = deletedAbsence is null
            ? null
            : $"EmploymentId={deletedAbsence.EmploymentId}; " +
              $"AbsenceTypeId={deletedAbsence.AbsenceTypeId}; " +
              $"StartDate={deletedAbsence.StartDate:yyyy-MM-dd}; " +
              $"EndDate={deletedAbsence.EndDate:yyyy-MM-dd}; " +
              $"FullDay={deletedAbsence.FullDay}; " +
              $"StartTime={deletedAbsence.StartTime?.ToString("HH:mm:ss.fffffff") ?? "null"}; " +
              $"EndTime={deletedAbsence.EndTime?.ToString("HH:mm:ss.fffffff") ?? "null"}";

        return auditLog.WriteAsync(
            httpContext,
            operation: "DELETE_EMPLOYEE_ABSENCE",
            objectType: "EmployeeAbsence",
            objectId: absenceId.ToString(),
            originalValue: originalValue,
            updatedValue: null,
            success: success,
            message: message,
            cancellationToken);
    }
}