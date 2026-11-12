using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeQueryEndpoints
{
    public static WebApplication MapEmployeeQueryEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/employees", SearchEmployeesAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("SearchEmployees");

        app.MapGet("/api/employees/{employeeId:int}", GetEmployeeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("GetEmployee");

        return app;
    }

    private static async Task<IResult> SearchEmployeesAsync(
        string? search,
        bool? isActive,
        EmployeeQueryRepository employees,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await employees.SearchAsync(
                search,
                isActive,
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "SEARCH",
                objectId: null,
                updatedValue: $"ResultCount={result.Count}; SearchProvided={!string.IsNullOrWhiteSpace(search)}; IsActiveFilter={isActive?.ToString() ?? "none"}",
                success: true,
                message: "A munkavállalók lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("EmployeeQueryEndpoints");
            logger.LogError(exception, "Hiba történt a munkavállalók lekérdezésekor.");

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "SEARCH",
                objectId: null,
                updatedValue: null,
                success: false,
                message: "A munkavállalók lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A munkavállalók lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetEmployeeAsync(
        int employeeId,
        EmployeeQueryRepository employees,
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
                operation: "READ",
                objectId: employeeId.ToString(),
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var employee = await employees.GetByIdAsync(
                employeeId,
                cancellationToken);

            if (employee is null)
            {
                const string message = "A munkavállaló nem található.";

                await WriteAuditAsync(
                    auditLog,
                    httpContext,
                    operation: "READ",
                    objectId: employeeId.ToString(),
                    updatedValue: null,
                    success: false,
                    message,
                    cancellationToken);

                return Results.NotFound(new { message });
            }

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ",
                objectId: employeeId.ToString(),
                updatedValue: null,
                success: true,
                message: "A munkavállaló lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(employee);
        }
        catch (SqlException exception) when (exception.Number == 50100)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ",
                objectId: employeeId.ToString(),
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50101)
        {
            const string message = "A munkavállaló nem található.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ",
                objectId: employeeId.ToString(),
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("EmployeeQueryEndpoints");
            logger.LogError(
                exception,
                "Hiba történt a munkavállaló lekérdezésekor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message = "A munkavállaló lekérdezése sikertelen.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ",
                objectId: employeeId.ToString(),
                updatedValue: null,
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
            objectType: "Employee",
            objectId: objectId,
            originalValue: null,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }
}