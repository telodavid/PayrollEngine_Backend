using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeTerminationEndpoints
{
    public static WebApplication MapEmployeeTerminationEndpoints(
        this WebApplication app)
    {
        app.MapPut(
                "/api/employees/{employeeId:int}/termination",
                TerminateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("TerminateEmployee");

        return app;
    }

    private static async Task<IResult> TerminateAsync(
        int employeeId,
        TerminateEmployeeRequest request,
        EmployeeTerminationRepository employees,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request.TerminationDate,
                success: false,
                message,
                updatedValue: null,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        var logger =
            loggerFactory.CreateLogger("EmployeeTerminationEndpoints");

        try
        {
            var result = await employees.TerminateAsync(
                employeeId,
                request.TerminationDate,
                cancellationToken);

            var updatedValue =
                $"IsActive=false; TerminationDate={request.TerminationDate:yyyy-MM-dd}; " +
                $"ClosedPayrollElements={result.ClosedPayrollElements}; " +
                $"DeletedFuturePayrollElements={result.DeletedFuturePayrollElements}";

            const string message = "A munkavállaló kiléptetése sikerült.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request.TerminationDate,
                success: true,
                message,
                updatedValue,
                cancellationToken);

            return Results.Ok(new
            {
                result.EmployeeId,
                result.TerminationDate,
                result.IsActive,
                result.ClosedPayrollElements,
                result.DeletedFuturePayrollElements,
                message
            });
        }
        catch (SqlException exception) when (exception.Number == 50201)
        {
            const string message = "A munkavállaló nem található.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request.TerminationDate,
                success: false,
                message,
                updatedValue: null,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50202)
        {
            const string message =
                "A munkavállaló belépési dátuma nincs megadva.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request.TerminationDate,
                success: false,
                message,
                updatedValue: null,
                cancellationToken);

            return Results.Conflict(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50203)
        {
            const string message = "A munkavállaló már inaktív.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request.TerminationDate,
                success: false,
                message,
                updatedValue: null,
                cancellationToken);

            return Results.Conflict(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50204)
        {
            const string message =
                "A kilépés dátuma nem lehet korábbi a belépés dátumánál.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request.TerminationDate,
                success: false,
                message,
                updatedValue: null,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkavállaló kiléptetésekor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message = "A munkavállaló kiléptetése sikertelen.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                request.TerminationDate,
                success: false,
                message,
                updatedValue: null,
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
        DateOnly terminationDate,
        bool success,
        string message,
        string? updatedValue,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: "TERMINATE",
            objectType: "Employee",
            objectId: employeeId > 0 ? employeeId.ToString() : null,
            originalValue: null,
            updatedValue: updatedValue ??
                $"TerminationDate={terminationDate:yyyy-MM-dd}",
            success: success,
            message: message,
            cancellationToken);
    }
}

public sealed record TerminateEmployeeRequest(DateOnly TerminationDate);