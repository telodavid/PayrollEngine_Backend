using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeBankAccountEndpoints
{
    public static WebApplication MapEmployeeBankAccountEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/employees/{employeeId:int}/payment-accounts",
                SetAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("SetEmployeePaymentAccount");
        app.MapGet(
        "/api/employees/{employeeId:int}/payment-accounts",
        GetByEmployeeIdAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
    .WithName("GetEmployeePaymentAccounts");

        app.MapDelete(
        "/api/employees/{employeeId:int}/payment-accounts/{employeeBankAccountId:int}",
        DeleteAsync)
    .RequireAuthorization(policy =>
        policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
    .WithName("DeleteEmployeePaymentAccount");

        return app;
    }

    private static async Task<IResult> SetAsync(
        int employeeId,
        SetEmployeeBankAccountRequest request,
        EmployeeBankAccountRepository accounts,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.BadRequest(new
                {
                    message = "Érvénytelen munkavállaló-azonosító."
                }),
                "Érvénytelen munkavállaló-azonosító.",
                cancellationToken);
        }

        if (request.AccountType is null ||
            !(request.AccountType.Equals(
                  "BANK_ACCOUNT",
                  StringComparison.OrdinalIgnoreCase)
              || request.AccountType.Equals(
                  "SZEP_CARD",
                  StringComparison.OrdinalIgnoreCase)))
        {
            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.BadRequest(new
                {
                    message = "A számlatípus BANK_ACCOUNT vagy SZEP_CARD lehet."
                }),
                "Érvénytelen számlatípus.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.AccountIdentifier) ||
            request.AccountIdentifier.Trim().Length > 50)
        {
            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.BadRequest(new
                {
                    message = "A számlaazonosító nem lehet üres, és legfeljebb 50 karakter lehet."
                }),
                "Érvénytelen számlaazonosító.",
                cancellationToken);
        }

        if (request.ValidFrom == default)
        {
            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.BadRequest(new
                {
                    message = "A kezdődátum megadása kötelező."
                }),
                "Hiányzó kezdődátum.",
                cancellationToken);
        }

        request = request with
        {
            AccountType = request.AccountType.Trim().ToUpperInvariant(),
            AccountIdentifier = request.AccountIdentifier.Trim()
        };

        var logger = loggerFactory.CreateLogger(
            "EmployeeBankAccountEndpoints");

        try
        {
            var result = await accounts.SetAsync(
                employeeId,
                request,
                cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                success: true,
                message: "A fizetési számla adatainak rögzítése sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                result.EmployeeBankAccountId,
                result.EmployeeId,
                result.AccountType,
                result.AccountIdentifier,
                result.ValidFrom,
                result.ValidTo,
                message = "A fizetési számla adatainak rögzítése sikerült."
            });
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
                "Hiba történt a fizetési számla rögzítésekor. EmployeeId: {EmployeeId}, AccountType: {AccountType}",
                employeeId,
                request.AccountType);

            return await AuditFailureAsync(
                httpContext,
                auditLog,
                employeeId,
                request,
                Results.Problem(
                    title: "A fizetési számla rögzítése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                "A fizetési számla rögzítése sikertelen.",
                cancellationToken);
        }
    }

    private static (IResult Response, string Message) MapSqlError(
        int errorNumber)
    {
        return errorNumber switch
        {
            50601 or 50602 or 50603 or 50605 or 50606 =>
                (
                    Results.BadRequest(new
                    {
                        message = "A megadott számlaadat vagy kezdődátum érvénytelen."
                    }),
                    "Érvénytelen számlaadat vagy kezdődátum."
                ),

            50604 =>
                (
                    Results.NotFound(new
                    {
                        message = "A munkavállaló nem található."
                    }),
                    "A munkavállaló nem található."
                ),

            _ =>
                (
                    Results.Problem(
                        title: "A fizetési számla rögzítése sikertelen.",
                        statusCode: StatusCodes.Status500InternalServerError),
                    "Váratlan adatbázishiba történt a fizetési számla rögzítésekor."
                )
        };
    }

    private static async Task<IResult> AuditFailureAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        int employeeId,
        SetEmployeeBankAccountRequest request,
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
            message,
            cancellationToken);

        return response;
    }

    private static Task WriteAuditAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        int employeeId,
        SetEmployeeBankAccountRequest request,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        var updatedValue =
            $"AccountType={request.AccountType ?? "NULL"}; " +
            "AccountIdentifier=[REDACTED]; " +
            $"ValidFrom={request.ValidFrom:yyyy-MM-dd}";

        return auditLog.WriteAsync(
            httpContext,
            operation: "SET_EMPLOYEE_PAYMENT_ACCOUNT",
            objectType: "EmployeeBankAccount",
            objectId: employeeId.ToString(),
            originalValue: null,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }

    private static async Task<IResult> DeleteAsync(
    int employeeId,
    int employeeBankAccountId,
    EmployeeBankAccountRepository accounts,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "EmployeeBankAccountEndpoints");

        EmployeeBankAccountListItem? existingItem = null;

        Task WriteDeleteAuditAsync(bool success, string message)
        {
            var originalValue = existingItem is null
                ? null
                : $"AccountType={existingItem.AccountType}; " +
                  "AccountIdentifier=[REDACTED]; " +
                  $"ValidFrom={existingItem.ValidFrom:yyyy-MM-dd}; " +
                  $"ValidTo={existingItem.ValidTo?.ToString("yyyy-MM-dd") ?? "NULL"}";

            return auditLog.WriteAsync(
                httpContext,
                operation: "DELETE_EMPLOYEE_PAYMENT_ACCOUNT",
                objectType: "EmployeeBankAccount",
                objectId: employeeBankAccountId.ToString(),
                originalValue: originalValue,
                updatedValue: null,
                success: success,
                message: message,
                cancellationToken: cancellationToken);
        }

        if (employeeId <= 0 || employeeBankAccountId <= 0)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy bankszámla-azonosító.";

            await WriteDeleteAuditAsync(false, message);
            return Results.BadRequest(new { message });
        }

        try
        {
            var items = await accounts.GetByEmployeeIdAsync(
                employeeId,
                cancellationToken);

            existingItem = items.FirstOrDefault(
                item => item.EmployeeBankAccountId == employeeBankAccountId);

            if (existingItem is null)
            {
                const string message =
                    "A bankszámla nem található ennél a dolgozónál.";

                await WriteDeleteAuditAsync(false, message);
                return Results.NotFound(new { message });
            }

            await accounts.DeleteAsync(
                employeeId,
                employeeBankAccountId,
                cancellationToken);

            const string successMessage = "A bankszámla törlése sikerült.";

            await WriteDeleteAuditAsync(true, successMessage);

            return Results.Ok(new
            {
                employeeId,
                employeeBankAccountId,
                message = successMessage
            });
        }
        catch (SqlException exception) when (exception.Number == 51001)
        {
            const string message =
                "Érvénytelen munkavállaló- vagy bankszámla-azonosító.";

            await WriteDeleteAuditAsync(false, message);
            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 51002)
        {
            const string message =
                "A bankszámla nem található ennél a dolgozónál.";

            await WriteDeleteAuditAsync(false, message);
            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a bankszámla törlésekor. EmployeeId: {EmployeeId}, AccountId: {AccountId}",
                employeeId,
                employeeBankAccountId);

            const string message = "A bankszámla törlése sikertelen.";

            await WriteDeleteAuditAsync(false, message);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetByEmployeeIdAsync(
    int employeeId,
    EmployeeBankAccountRepository accounts,
    AuditLogService auditLog,
    ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYMENT_ACCOUNTS",
                objectType: "EmployeeBankAccount",
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
            "EmployeeBankAccountEndpoints");

        try
        {
            var result = await accounts.GetByEmployeeIdAsync(
                employeeId,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYMENT_ACCOUNTS",
                objectType: "EmployeeBankAccount",
                objectId: employeeId.ToString(),
                originalValue: null,
                updatedValue: $"Records={result.Count}",
                success: true,
                message: "A fizetési számlák lekérdezése sikerült.",
                cancellationToken: cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50601)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen munkavállaló-azonosító."
            });
        }
        catch (SqlException exception) when (exception.Number == 50604)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYMENT_ACCOUNTS",
                objectType: "EmployeeBankAccount",
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
                "Hiba történt a fizetési számlák lekérdezésekor. EmployeeId: {EmployeeId}",
                employeeId);

            await auditLog.WriteAsync(
                httpContext,
                operation: "GET_EMPLOYEE_PAYMENT_ACCOUNTS",
                objectType: "EmployeeBankAccount",
                objectId: employeeId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A fizetési számlák lekérdezése sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A fizetési számlák lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}