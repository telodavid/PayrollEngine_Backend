using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.CompanyMaintenanceApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class CompanyMaintenanceEndpoints
{
    public static WebApplication MapCompanyMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/companies", SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("SearchCompanies");

        app.MapGet("/api/companies/{companyId:int}", GetByIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("GetCompany");

        app.MapPost("/api/companies", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("CreateCompany");

        app.MapPut("/api/companies/{companyId:int}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("UpdateCompany");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        CompanyMaintenanceRepository companies,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CompanyMaintenanceEndpoints");

        try
        {
            var result = await companies.SearchAsync(search, cancellationToken);

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "READ_COMPANIES",
                objectId: null,
                updatedValue: $"Search={search ?? "null"}",
                success: true,
                message: "A cégek lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Hiba történt a cégek lekérdezésekor.");

            const string message = "A cégek lekérdezése sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "READ_COMPANIES",
                objectId: null,
                updatedValue: $"Search={search ?? "null"}",
                success: false,
                message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetByIdAsync(
        int companyId,
        CompanyMaintenanceRepository companies,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CompanyMaintenanceEndpoints");

        if (companyId <= 0)
        {
            const string message = "Érvénytelen cégazonosító.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "READ_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        try
        {
            var result = await companies.GetByIdAsync(companyId, cancellationToken);

            if (result is null)
            {
                const string message = "A cég nem található.";

                await WriteAuditAsync(
                    auditLog, httpContext,
                    operation: "READ_COMPANY",
                    objectId: companyId.ToString(),
                    updatedValue: null,
                    success: false,
                    message,
                    cancellationToken);

                return Results.NotFound(new { message });
            }

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "READ_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: null,
                success: true,
                message: "A cég lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception) when (exception.Number == 50302)
        {
            const string message = "Érvénytelen cégazonosító.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "READ_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50303)
        {
            const string message = "A cég nem található.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "READ_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a cég lekérdezésekor. CompanyId: {CompanyId}",
                companyId);

            const string message = "A cég lekérdezése sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "READ_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: null,
                success: false,
                message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateAsync(
        SaveCompanyRequest request,
        CompanyMaintenanceRepository companies,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CompanyMaintenanceEndpoints");
        var validationError = Validate(request);

        if (validationError is not null)
        {
            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "CREATE_COMPANY",
                objectId: null,
                updatedValue: Describe(request),
                success: false,
                validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var companyId = await companies.CreateAsync(request, cancellationToken);

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "CREATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: true,
                message: "A cég létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/companies/{companyId}",
                new
                {
                    companyId,
                    request.CompanyName,
                    request.CompanyAddress,
                    request.BankAccountNumber,
                    request.TaxId,
                    request.PhoneNumber,
                    message = "A cég létrejött."
                });
        }
        catch (SqlException exception) when (exception.Number == 50301)
        {
            const string message = "A cégnév nem lehet üres.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "CREATE_COMPANY",
                objectId: null,
                updatedValue: Describe(request),
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a cég létrehozásakor. CompanyName: {CompanyName}",
                request.CompanyName);

            const string message = "A cég létrehozása sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "CREATE_COMPANY",
                objectId: null,
                updatedValue: Describe(request),
                success: false,
                message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int companyId,
        SaveCompanyRequest request,
        CompanyMaintenanceRepository companies,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CompanyMaintenanceEndpoints");

        if (companyId <= 0)
        {
            const string message = "Érvénytelen cégazonosító.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "UPDATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        var validationError = Validate(request);

        if (validationError is not null)
        {
            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "UPDATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: false,
                validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            await companies.UpdateAsync(companyId, request, cancellationToken);

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "UPDATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: true,
                message: "A cég adatai módosultak.",
                cancellationToken);

            return Results.Ok(new
            {
                companyId,
                request.CompanyName,
                request.CompanyAddress,
                request.BankAccountNumber,
                request.TaxId,
                request.PhoneNumber,
                message = "A cég adatai módosultak."
            });
        }
        catch (SqlException exception) when (exception.Number == 50302)
        {
            const string message = "Érvénytelen cégazonosító.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "UPDATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50303)
        {
            const string message = "A cég nem található.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "UPDATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: false,
                message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (SqlException exception) when (exception.Number == 50301)
        {
            const string message = "A cégnév nem lehet üres.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "UPDATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: false,
                message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a cég módosításakor. CompanyId: {CompanyId}",
                companyId);

            const string message = "A cég módosítása sikertelen.";

            await WriteAuditAsync(
                auditLog, httpContext,
                operation: "UPDATE_COMPANY",
                objectId: companyId.ToString(),
                updatedValue: Describe(request),
                success: false,
                message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? Validate(SaveCompanyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName) ||
            request.CompanyName.Length > 200)
        {
            return "A cégnév nem lehet üres, és legfeljebb 200 karakter lehet.";
        }

        if (request.CompanyAddress?.Length > 500)
        {
            return "A cég címe legfeljebb 500 karakter lehet.";
        }

        if (request.BankAccountNumber?.Length > 50)
        {
            return "A bankszámlaszám legfeljebb 50 karakter lehet.";
        }

        if (request.TaxId?.Length > 30)
        {
            return "Az adószám legfeljebb 30 karakter lehet.";
        }

        if (request.PhoneNumber?.Length > 30)
        {
            return "A telefonszám legfeljebb 30 karakter lehet.";
        }

        return null;
    }

    private static string Describe(SaveCompanyRequest request)
    {
        return
            $"CompanyName={request.CompanyName}; " +
            $"CompanyAddress={(request.CompanyAddress is null ? "null" : "[megadva]")}; " +
            $"BankAccountNumber={(request.BankAccountNumber is null ? "null" : "[megadva]")}; " +
            $"TaxId={(request.TaxId is null ? "null" : "[megadva]")}; " +
            $"PhoneNumber={(request.PhoneNumber is null ? "null" : "[megadva]")}";
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
            objectType: "Company",
            objectId: objectId,
            originalValue: null,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken);
    }
}