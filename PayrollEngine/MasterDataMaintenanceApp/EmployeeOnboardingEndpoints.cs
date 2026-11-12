using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeOnboardingEndpoints
{
    public static WebApplication MapEmployeeOnboardingEndpoints(
        this WebApplication app)
    {
        app.MapPost("/api/employees/onboard", OnboardEmployeeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("OnboardEmployee");

        return app;
    }

    private static async Task<IResult> OnboardEmployeeAsync(
        EmployeeOnboardingRequest request,
        EmployeeOnboardingRepository employees,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);

        if (validationError is not null)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                objectId: null,
                success: false,
                message: validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        var logger = loggerFactory.CreateLogger(
            "EmployeeOnboardingEndpoints");

        try
        {
            var employeeId = await employees.OnboardAsync(
                request,
                cancellationToken);

            const string successMessage =
                "A munkavállaló beléptetése sikerült.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                employeeId.ToString(),
                success: true,
                successMessage,
                cancellationToken);

            return Results.Created(
                $"/api/employees/{employeeId}",
                new
                {
                    employeeId,
                    request.JoinDate,
                    payrollRunFrom = request.JoinDate,
                    request.CompanyId,
                    request.WorkScheduleId,
                    request.PayrollElementId,
                    message = successMessage
                });
        }
        catch (SqlException exception) when (exception.Number == 50080)
        {
            return await BadRequestAndAuditAsync(
                auditLog,
                httpContext,
                "Minden beléptetési adat megadása kötelező.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50081)
        {
            return await BadRequestAndAuditAsync(
                auditLog,
                httpContext,
                "Érvénytelen beléptetési adat.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50082)
        {
            return await NotFoundAndAuditAsync(
                auditLog,
                httpContext,
                "A megadott cég nem található.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50083)
        {
            return await NotFoundAndAuditAsync(
                auditLog,
                httpContext,
                "A megadott aktív bérelem nem található.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50084)
        {
            return await BadRequestAndAuditAsync(
                auditLog,
                httpContext,
                "A megadott munkarend nem aktív vagy nincs teljesen konfigurálva.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            return await ConflictAndAuditAsync(
                auditLog,
                httpContext,
                "A megadott személyes, adó- vagy TAJ-azonosító már szerepel a rendszerben.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkavállaló beléptetésekor.");

            const string errorMessage =
                "A munkavállaló beléptetése sikertelen.";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                objectId: null,
                success: false,
                errorMessage,
                cancellationToken);

            return Results.Problem(
                title: errorMessage,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? ValidateRequest(
        EmployeeOnboardingRequest request)
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

        if (!request.BirthDate.HasValue ||
            !request.JoinDate.HasValue ||
            request.BirthDate > request.JoinDate)
        {
            return "Érvényes születési és belépési dátumot kell megadni.";
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

        if (request.CompanyId is null or <= 0 ||
            request.WorkScheduleId is null or <= 0 ||
            request.PayrollElementId is null or <= 0)
        {
            return "Céget, munkarendet és bérelemet is ki kell választani.";
        }

        if (request.BaseSalaryAmount is null or <= 0)
        {
            return "Az alapbér összege kötelező, és nagyobb kell legyen nullánál.";
        }

        return null;
    }

    private static async Task<IResult> BadRequestAndAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            auditLog, httpContext, null, false, message, cancellationToken);

        return Results.BadRequest(new { message });
    }

    private static async Task<IResult> NotFoundAndAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            auditLog, httpContext, null, false, message, cancellationToken);

        return Results.NotFound(new { message });
    }

    private static async Task<IResult> ConflictAndAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            auditLog, httpContext, null, false, message, cancellationToken);

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
            operation: "ONBOARD",
            objectType: "Employee",
            objectId: objectId,
            originalValue: null,
            updatedValue: "Employee, employment, work schedule and base salary.",
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }
}