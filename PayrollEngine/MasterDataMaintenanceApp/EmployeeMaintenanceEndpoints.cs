using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeMaintenanceEndpoints
{
    public static WebApplication MapEmployeeMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPut("/api/employees/{employeeId:int}", UpdateEmployeeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("UpdateEmployee");

        return app;
    }

    private static async Task<IResult> UpdateEmployeeAsync(
        int employeeId,
        UpdateEmployeeRequest request,
        EmployeeMaintenanceRepository employees,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId.ToString(),
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        var validationError = ValidateRequest(request);

        if (validationError is not null)
        {
            await WriteAuditAsync(
                auditLog, httpContext, employeeId.ToString(),
                success: false, validationError, cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        var logger = loggerFactory.CreateLogger(
            "EmployeeMaintenanceEndpoints");

        try
        {
            await employees.UpdateAsync(
                employeeId,
                request,
                cancellationToken);

            const string message = "A munkavállaló adatai módosultak.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId.ToString(),
                success: true, message, cancellationToken);

            return Results.Ok(new
            {
                employeeId,
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.PersonalId,
                request.TaxId,
                request.TAJId,
                request.Address,
                message
            });
        }
        catch (SqlException exception) when (exception.Number == 50085)
        {
            return await BadRequestAndAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                "Minden szerkeszthető munkavállalói adat megadása kötelező.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50086)
        {
            return await BadRequestAndAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                "A születési dátum nem lehet későbbi a belépési dátumnál.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50087)
        {
            const string message = "A munkavállaló nem található.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId.ToString(),
                success: false, message, cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            return await ConflictAndAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                "A megadott személyi, adó- vagy TAJ-azonosító már szerepel a rendszerben.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkavállaló módosításakor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message = "A munkavállaló módosítása sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId.ToString(),
                success: false, message, cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? ValidateRequest(UpdateEmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            request.FirstName.Length > 100)
        {
            return "A keresztnév kötelező, és legfeljebb 100 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(request.LastName) ||
            request.LastName.Length > 100)
        {
            return "A vezetéknév kötelező, és legfeljebb 100 karakter lehet.";
        }

        if (!request.BirthDate.HasValue)
        {
            return "A születési dátum megadása kötelező.";
        }

        if (string.IsNullOrWhiteSpace(request.PersonalId) ||
            request.PersonalId.Length > 30)
        {
            return "A személyi azonosító kötelező, és legfeljebb 30 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(request.TaxId) ||
            request.TaxId.Length > 30)
        {
            return "Az adóazonosító kötelező, és legfeljebb 30 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(request.TAJId) ||
            request.TAJId.Length > 30)
        {
            return "A TAJ-szám kötelező, és legfeljebb 30 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(request.Address) ||
            request.Address.Length > 500)
        {
            return "A cím kötelező, és legfeljebb 500 karakter lehet.";
        }

        return null;
    }

    private static async Task<IResult> BadRequestAndAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            auditLog, httpContext, employeeId.ToString(),
            success: false, message, cancellationToken);

        return Results.BadRequest(new { message });
    }

    private static async Task<IResult> ConflictAndAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            auditLog, httpContext, employeeId.ToString(),
            success: false, message, cancellationToken);

        return Results.Conflict(new { message });
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string? objectId,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: "UPDATE",
            objectType: "Employee",
            objectId: objectId,
            originalValue: null,
            updatedValue: "Employee personal data update requested.",
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }
}