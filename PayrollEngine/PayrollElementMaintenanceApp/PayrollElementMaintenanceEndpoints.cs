using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.PayrollElementMaintenanceApp;

public static class PayrollElementMaintenanceEndpoints
{
    public static WebApplication MapPayrollElementMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/payroll-elements", SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
            .WithName("SearchPayrollElements");

        app.MapGet("/api/payroll-elements/{payrollElementId:int}", GetByIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
            .WithName("GetPayrollElement");

        app.MapPost("/api/payroll-elements", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
            .WithName("CreatePayrollElement");

        app.MapPut("/api/payroll-elements/{payrollElementId:int}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_ELEMENT_MAINTENANCE"))
            .WithName("UpdatePayrollElement");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        bool? isActive,
        HttpContext httpContext,
        PayrollElementMaintenanceRepository payrollElements,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await payrollElements.SearchAsync(
                search,
                isActive,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollElement",
                objectId: null,
                originalValue: null,
                updatedValue: $"Count={result.Count}; Search={search ?? "ALL"}; IsActive={isActive?.ToString() ?? "ALL"}",
                success: true,
                message: "A bérelemek lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollElementMaintenanceEndpoints")
                .LogError(exception, "Hiba történt a bérelemek lekérdezésekor.");

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollElement",
                objectId: null,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A bérelemek lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A bérelemek lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetByIdAsync(
        int payrollElementId,
        HttpContext httpContext,
        PayrollElementMaintenanceRepository payrollElements,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (payrollElementId <= 0)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "Érvénytelen bérelem-azonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen bérelem-azonosító."
            });
        }

        try
        {
            var result = await payrollElements.GetByIdAsync(
                payrollElementId,
                cancellationToken);

            if (result is null)
            {
                await auditLog.WriteAsync(
                    httpContext,
                    operation: "READ",
                    objectType: "PayrollElement",
                    objectId: payrollElementId.ToString(),
                    originalValue: null,
                    updatedValue: null,
                    success: false,
                    message: "A bérelem nem található.",
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = "A bérelem nem található."
                });
            }

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: $"Code={result.Code}; Name={result.Name}; IsActive={result.IsActive}",
                success: true,
                message: "A bérelem lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50402)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A bérelem nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A bérelem nem található."
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollElementMaintenanceEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a bérelem lekérdezésekor. PayrollElementId: {PayrollElementId}",
                    payrollElementId);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A bérelem lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A bérelem lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateAsync(
        CreatePayrollElementRequest request,
        HttpContext httpContext,
        PayrollElementMaintenanceRepository payrollElements,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request.Code, request.Name);

        if (validationError is not null)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "PayrollElement",
                objectId: null,
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}",
                success: false,
                message: validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var payrollElementId = await payrollElements.CreateAsync(
                request,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}; IsActive=true",
                success: true,
                message: "A bérelem létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/payroll-elements/{payrollElementId}",
                new
                {
                    payrollElementId,
                    request.Code,
                    request.Name,
                    isActive = true,
                    message = "A bérelem létrejött."
                });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "PayrollElement",
                objectId: null,
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}",
                success: false,
                message: "Ez a bérelemkód már létezik.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez a bérelemkód már létezik."
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollElementMaintenanceEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a bérelem létrehozásakor. Code: {Code}",
                    request.Code);

            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "PayrollElement",
                objectId: null,
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}",
                success: false,
                message: "A bérelem létrehozása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A bérelem létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int payrollElementId,
        UpdatePayrollElementRequest request,
        HttpContext httpContext,
        PayrollElementMaintenanceRepository payrollElements,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (payrollElementId <= 0)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "Érvénytelen bérelem-azonosító.",
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen bérelem-azonosító."
            });
        }

        var validationError = Validate(request.Code, request.Name);

        if (validationError is not null)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}; IsActive={request.IsActive}",
                success: false,
                message: validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var result = await payrollElements.UpdateAsync(
                payrollElementId,
                request,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: $"Code={result.Code}; Name={result.Name}; IsActive={result.IsActive}",
                success: true,
                message: "A bérelem adatai módosultak.",
                cancellationToken);

            return Results.Ok(new
            {
                result.PayrollElementId,
                result.Code,
                result.Name,
                result.IsActive,
                message = "A bérelem adatai módosultak."
            });
        }
        catch (SqlException exception) when (exception.Number == 50402)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}; IsActive={request.IsActive}",
                success: false,
                message: "A bérelem nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A bérelem nem található."
            });
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}; IsActive={request.IsActive}",
                success: false,
                message: "Ez a bérelemkód már használatban van.",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "Ez a bérelemkód már használatban van."
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollElementMaintenanceEndpoints")
                .LogError(
                    exception,
                    "Hiba történt a bérelem módosításakor. PayrollElementId: {PayrollElementId}",
                    payrollElementId);

            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "PayrollElement",
                objectId: payrollElementId.ToString(),
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}; IsActive={request.IsActive}",
                success: false,
                message: "A bérelem módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A bérelem módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? Validate(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
        {
            return "A bérelem kódja nem lehet üres, és legfeljebb 50 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return "A bérelem neve nem lehet üres, és legfeljebb 100 karakter lehet.";
        }

        return null;
    }
}