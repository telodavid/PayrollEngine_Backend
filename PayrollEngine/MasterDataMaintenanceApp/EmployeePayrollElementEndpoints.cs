using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeePayrollElementEndpoints
{
    public static WebApplication MapEmployeePayrollElementEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/employees/{employeeId:int}/payroll-elements",
                SetAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("SetEmployeePayrollElement");
        app.MapGet(
        "/api/employees/{employeeId:int}/payroll-elements",
        GetByEmployeeIdAsync)
        .RequireAuthorization(policy =>
        policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
        .WithName("GetEmployeePayrollElements");

        app.MapDelete(
        "/api/employees/{employeeId:int}/payroll-elements/{employmentPayrollElementId:int}",
        DeleteAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
    .WithName("DeleteEmployeePayrollElement");

        return app;
    }

    private static async Task<IResult> SetAsync(
        int employeeId,
        SetEmployeePayrollElementRequest request,
        EmployeePayrollElementRepository payrollElements,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0 ||
            request.EmploymentId <= 0 ||
            request.PayrollElementId <= 0)
        {
            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.BadRequest(new
                {
                    message = "Érvénytelen munkavállaló-, munkaviszony- vagy bérelem-azonosító."
                }),
                "Érvénytelen azonosító.",
                cancellationToken);
        }

        if (request.ValidTo.HasValue &&
            request.ValidTo.Value < request.ValidFrom)
        {
            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.BadRequest(new
                {
                    message = "Az érvényesség vége nem lehet korábbi a kezdeténél."
                }),
                "Érvénytelen érvényességi időszak.",
                cancellationToken);
        }

        var logger = loggerFactory.CreateLogger(
            "EmployeePayrollElementEndpoints");

        try
        {
            var result = await payrollElements.SetAsync(
                employeeId,
                request,
                cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                success: !result.RequiresRetroPayroll,
                message: result.RequiresRetroPayroll
                    ? "Visszaszámfejtési megerősítés szükséges; az adat nem módosult."
                    : result.Message,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception)
        {
            var (response, message) = MapSqlError(exception.Number);

            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                response,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkavállaló bérelemének rögzítésekor. EmployeeId: {EmployeeId}, PayrollElementId: {PayrollElementId}",
                employeeId,
                request.PayrollElementId);

            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.Problem(
                    title: "A bérelem rögzítése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                "A bérelem rögzítése sikertelen.",
                cancellationToken);
        }
    }

    private static (IResult Response, string Message) MapSqlError(
        int errorNumber)
    {
        return errorNumber switch
        {
            50501 or 50502 or 50506 or 50507 =>
                (
                    Results.BadRequest(new
                    {
                        message = "A megadott bérelemadatok vagy dátumok érvénytelenek."
                    }),
                    "Érvénytelen bérelemadat vagy dátum."
                ),

            50503 =>
                (
                    Results.NotFound(new
                    {
                        message = "A munkavállaló nem található."
                    }),
                    "A munkavállaló nem található."
                ),

            50504 =>
                (
                    Results.NotFound(new
                    {
                        message = "A munkaviszony nem található ennél a munkavállalónál."
                    }),
                    "A munkaviszony nem található ennél a munkavállalónál."
                ),

            50505 =>
                (
                    Results.NotFound(new
                    {
                        message = "Az aktív bérelem nem található."
                    }),
                    "Az aktív bérelem nem található."
                ),

            50508 =>
                (
                    Results.Conflict(new
                    {
                        message = "A bérelem időszaka átfed egy későbbi hozzárendeléssel."
                    }),
                    "A bérelem időszaka átfed egy másik hozzárendeléssel."
                ),

            _ =>
                (
                    Results.Problem(
                        title: "A bérelem rögzítése sikertelen.",
                        statusCode: StatusCodes.Status500InternalServerError),
                    "Váratlan adatbázishiba történt a bérelem rögzítésekor."
                )
        };
    }

    private static async Task<IResult> AuditFailureAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        int employeeId,
        SetEmployeePayrollElementRequest request,
        IResult response,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            httpContext,
            auditLog,
            employeeId,
            request,
            success: false,
            message: message,
            cancellationToken: cancellationToken);

        return response;
    }

    private static Task WriteAuditAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        int employeeId,
        SetEmployeePayrollElementRequest request,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        var updatedValue =
            $"EmploymentId={request.EmploymentId}; " +
            $"PayrollElementId={request.PayrollElementId}; " +
            $"ValidFrom={request.ValidFrom:yyyy-MM-dd}; " +
            $"ValidTo={request.ValidTo?.ToString("yyyy-MM-dd") ?? "NULL"}; " +
            $"Amount={request.Amount}; " +
            $"RetroPayrollRunFrom={request.RetroPayrollRunFrom?.ToString("yyyy-MM-dd") ?? "NULL"}";

        return auditLog.WriteAsync(
            httpContext,
            operation: "SET_EMPLOYEE_PAYROLL_ELEMENT",
            objectType: "EmploymentPayrollElement",
            objectId: employeeId.ToString(),
            originalValue: null,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }
    private static async Task<IResult> DeleteAsync(
    int employeeId,
    int employmentPayrollElementId,
    EmployeePayrollElementRepository payrollElements,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmployeePayrollElementEndpoints");

        EmployeePayrollElementListItem? existingItem = null;

        async Task WriteAuditAsync(
            bool success,
            string message,
            DateOnly? payrollRunFrom = null)
        {
            var originalValue = existingItem is null
                ? null
                : $"EmploymentId={existingItem.EmploymentId}; " +
                  $"PayrollElementId={existingItem.PayrollElementId}; " +
                  $"PayrollElementCode={existingItem.PayrollElementCode}; " +
                  $"ValidFrom={existingItem.ValidFrom:yyyy-MM-dd}; " +
                  $"ValidTo={existingItem.ValidTo?.ToString("yyyy-MM-dd") ?? "NULL"}; " +
                  $"Amount={existingItem.Amount}";

            await auditLog.WriteAsync(
                httpContext,
                operation: "DELETE_EMPLOYEE_PAYROLL_ELEMENT",
                objectType: "EmploymentPayrollElement",
                objectId: employmentPayrollElementId.ToString(),
                originalValue: originalValue,
                updatedValue: payrollRunFrom.HasValue
                    ? $"PayrollRunFrom={payrollRunFrom.Value:yyyy-MM-dd}"
                    : null,
                success: success,
                message: message,
                cancellationToken: cancellationToken);
        }

        if (employeeId <= 0 || employmentPayrollElementId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy bérelem-hozzárendelés-azonosító.";

            await WriteAuditAsync(false, message);
            return Results.BadRequest(new { message });
        }

        try
        {
            var items = await payrollElements.GetByEmployeeIdAsync(
                employeeId,
                cancellationToken);

            existingItem = items.FirstOrDefault(
                item => item.EmploymentPayrollElementId ==
                        employmentPayrollElementId);

            if (existingItem is null)
            {
                const string message =
                    "A bérelem-hozzárendelés nem található ennél a dolgozónál.";

                await WriteAuditAsync(false, message);
                return Results.NotFound(new { message });
            }

            var result = await payrollElements.DeleteAsync(
                employeeId,
                employmentPayrollElementId,
                cancellationToken);

            const string successMessage = "A bérelem törlése sikerült.";

            await WriteAuditAsync(
                true,
                successMessage,
                result.PayrollRunFrom);

            return Results.Ok(new
            {
                result.EmployeeId,
                result.EmploymentPayrollElementId,
                result.PayrollRunFrom,
                message = successMessage
            });
        }
        catch (SqlException exception) when (exception.Number == 50711)
        {
            const string message =
                "A bérelem-hozzárendelés nem található ennél a dolgozónál.";

            await WriteAuditAsync(false, message);
            return Results.NotFound(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50712)
        {
            const string message =
                "A dolgozó belépési dátuma nem található.";

            await WriteAuditAsync(false, message);
            return Results.Conflict(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a bérelem törlésekor. EmployeeId: {EmployeeId}, AssignmentId: {AssignmentId}",
                employeeId,
                employmentPayrollElementId);

            const string message = "A bérelem törlése sikertelen.";

            await WriteAuditAsync(false, message);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    private static async Task<IResult> GetByEmployeeIdAsync(
    int employeeId,
    EmployeePayrollElementRepository payrollElements,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYROLL_ELEMENTS",
                objectType: "EmploymentPayrollElement",
                objectId: employeeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "Érvénytelen munkavállaló-azonosító.",
                cancellationToken: cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen munkavállaló-azonosító."
            });
        }

        var logger = loggerFactory.CreateLogger(
            "EmployeePayrollElementEndpoints");

        try
        {
            var result = await payrollElements.GetByEmployeeIdAsync(
                employeeId,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYROLL_ELEMENTS",
                objectType: "EmploymentPayrollElement",
                objectId: employeeId.ToString(),
                originalValue: null,
                updatedValue: $"Records={result.Count}",
                success: true,
                message: "A munkavállaló bérelemeinek lekérdezése sikerült.",
                cancellationToken: cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50501)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen munkavállaló-azonosító."
            });
        }
        catch (SqlException exception) when (exception.Number == 50502)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYROLL_ELEMENTS",
                objectType: "EmploymentPayrollElement",
                objectId: employeeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A munkavállaló nem található.",
                cancellationToken: cancellationToken);

            return Results.NotFound(new
            {
                message = "A munkavállaló nem található."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkavállaló bérelemeinek lekérdezésekor. EmployeeId: {EmployeeId}",
                employeeId);

            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYROLL_ELEMENTS",
                objectType: "EmploymentPayrollElement",
                objectId: employeeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A bérelemek lekérdezése sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A bérelemek lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}