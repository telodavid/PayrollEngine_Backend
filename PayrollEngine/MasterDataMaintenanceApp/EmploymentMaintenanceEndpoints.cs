using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmploymentMaintenanceEndpoints
{
    public static WebApplication MapEmploymentMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/employees/{employeeId:int}/employment",
                ChangeCompanyAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("ChangeEmploymentCompany");
        app.MapGet(
        "/api/employees/{employeeId:int}/employments",
        GetHistoryAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
    .WithName("GetEmploymentHistory");

        app.MapDelete(
        "/api/employees/{employeeId:int}/employments/{employmentId:int}",
        DeleteAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
    .WithName("DeleteEmployment");

        return app;
    }

    private static async Task<IResult> ChangeCompanyAsync(
        int employeeId,
        ChangeEmploymentCompanyRequest request,
        EmploymentMaintenanceRepository employments,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmploymentMaintenanceEndpoints");

        if (employeeId <= 0 || request.CompanyId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy cégazonosító.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (request.ValidFrom == default)
        {
            const string message = "A kezdődátum megadása kötelező.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        if (request.RetroPayrollRunFrom.HasValue &&
            request.RetroPayrollRunFrom.Value > request.ValidFrom)
        {
            const string message =
                "A visszaszámfejtés kezdődátuma nem lehet későbbi a módosítás dátumánál.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, request,
                success: false, message, cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var result = await employments.ChangeCompanyAsync(
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
                    result.RequiresRetroPayroll,
                    result.SuggestedPayrollRunFrom,
                    result.Message
                });
            }

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50201)
        {
            return await SqlErrorAsync(
                StatusCodes.Status404NotFound,
                "A munkavállaló nem található, vagy nincs belépési dátuma.");
        }
        catch (SqlException exception) when (exception.Number == 50202)
        {
            return await SqlErrorAsync(
                StatusCodes.Status400BadRequest,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number == 50203)
        {
            return await SqlErrorAsync(
                StatusCodes.Status404NotFound,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number == 50204)
        {
            return await SqlErrorAsync(
                StatusCodes.Status409Conflict,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number == 50205)
        {
            return await SqlErrorAsync(
                StatusCodes.Status409Conflict,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number == 50206)
        {
            return await SqlErrorAsync(
                StatusCodes.Status400BadRequest,
                exception.Message);
        }
        catch (SqlException exception) when (exception.Number is 50207 or 50208)
        {
            return await SqlErrorAsync(
                StatusCodes.Status409Conflict,
                exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkaviszony módosításakor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message = "A munkaviszony módosítása sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext, employeeId, request,
                success: false, message, cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }

        async Task<IResult> SqlErrorAsync(int statusCode, string message)
        {
            await WriteAuditAsync(
                auditLog, httpContext, employeeId, request,
                success: false, message, cancellationToken);

            return Results.Json(new { message }, statusCode: statusCode);
        }
    }

    private static async Task<IResult> DeleteAsync(
    int employeeId,
    int employmentId,
    EmploymentMaintenanceRepository employments,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmploymentMaintenanceEndpoints");

        EmploymentHistoryItem? existingItem = null;

        Task WriteDeleteAuditAsync(
            bool success,
            string message,
            DateOnly? payrollRunFrom = null)
        {
            var originalValue = existingItem is null
                ? null
                : $"CompanyId={existingItem.CompanyId}; " +
                  $"CompanyName={existingItem.CompanyName}; " +
                  $"ValidFrom={existingItem.ValidFrom:yyyy-MM-dd}; " +
                  $"ValidTo={existingItem.ValidTo?.ToString("yyyy-MM-dd") ?? "NULL"}";

            return auditLog.WriteAsync(
                httpContext,
                operation: "DELETE_EMPLOYMENT",
                objectType: "Employment",
                objectId: employmentId.ToString(),
                originalValue: originalValue,
                updatedValue: payrollRunFrom.HasValue
                    ? $"PayrollRunFrom={payrollRunFrom.Value:yyyy-MM-dd}"
                    : null,
                success: success,
                message: message,
                cancellationToken: cancellationToken);
        }

        if (employeeId <= 0 || employmentId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy munkaviszony-azonosító.";

            await WriteDeleteAuditAsync(false, message);
            return Results.BadRequest(new { message });
        }

        try
        {
            var history = await employments.GetByEmployeeIdAsync(
                employeeId,
                cancellationToken);

            existingItem = history.FirstOrDefault(
                item => item.EmploymentId == employmentId);

            if (existingItem is null)
            {
                const string message =
                    "A munkaviszony nem található ennél a dolgozónál.";

                await WriteDeleteAuditAsync(false, message);
                return Results.NotFound(new { message });
            }

            var result = await employments.DeleteAsync(
                employeeId,
                employmentId,
                cancellationToken);

            const string successMessage = "A munkaviszony törlése sikerült.";

            await WriteDeleteAuditAsync(
                true,
                successMessage,
                result.PayrollRunFrom);

            return Results.Ok(new
            {
                result.EmployeeId,
                result.EmploymentId,
                result.PayrollRunFrom,
                message = successMessage
            });
        }
        catch (SqlException exception) when (exception.Number == 50801)
        {
            const string message =
                "A munkaviszony nem található ennél a dolgozónál.";

            await WriteDeleteAuditAsync(false, message);
            return Results.NotFound(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50802)
        {
            const string message =
                "A munkaviszonyhoz még bérelem, munkarend vagy távollét tartozik. Ezeket előbb töröld.";

            await WriteDeleteAuditAsync(false, message);
            return Results.Conflict(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkaviszony törlésekor. EmployeeId: {EmployeeId}, EmploymentId: {EmploymentId}",
                employeeId,
                employmentId);

            const string message = "A munkaviszony törlése sikertelen.";

            await WriteDeleteAuditAsync(false, message);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetHistoryAsync(
    int employeeId,
    EmploymentMaintenanceRepository employments,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmploymentMaintenanceEndpoints");

        if (employeeId <= 0)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteHistoryAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var result = await employments.GetByEmployeeIdAsync(
                employeeId,
                cancellationToken);

            await WriteHistoryAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                success: true,
                "A munkaviszony-előzmények lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50201)
        {
            const string message = "A munkavállaló nem található.";

            await WriteHistoryAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50209)
        {
            const string message = "Érvénytelen munkavállaló-azonosító.";

            await WriteHistoryAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a munkaviszony-előzmények lekérdezésekor. EmployeeId: {EmployeeId}",
                employeeId);

            const string message =
                "A munkaviszony-előzmények lekérdezése sikertelen.";

            await WriteHistoryAuditAsync(
                auditLog,
                httpContext,
                employeeId,
                success: false,
                message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Task WriteHistoryAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: "READ_EMPLOYMENT_HISTORY",
            objectType: "Employment",
            objectId: employeeId.ToString(),
            originalValue: null,
            updatedValue: null,
            success: success,
            message: message,
            cancellationToken);
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        int employeeId,
        ChangeEmploymentCompanyRequest request,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: "CHANGE_EMPLOYMENT_COMPANY",
            objectType: "Employment",
            objectId: employeeId.ToString(),
            originalValue: null,
            updatedValue:
                $"CompanyId={request.CompanyId}; " +
                $"ValidFrom={request.ValidFrom:yyyy-MM-dd}; " +
                $"RetroPayrollRunFrom=" +
                $"{request.RetroPayrollRunFrom?.ToString("yyyy-MM-dd") ?? "null"}",
            success: success,
            message: message,
            cancellationToken);
    }
}