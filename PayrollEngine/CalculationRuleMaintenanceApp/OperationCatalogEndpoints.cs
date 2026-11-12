using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public static class OperationCatalogEndpoints
{
    public static WebApplication MapOperationCatalogEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/calculation-rules/operations", GetOperationsAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "CALCULATION_RULE_MAINTENANCE"))
            .WithName("GetCalculationOperations");

        return app;
    }

    private static async Task<IResult> GetOperationsAsync(
        OperationCatalogService catalogService,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var catalog = catalogService.Catalog;

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "OperationCatalog",
                objectId: null,
                originalValue: null,
                updatedValue: $"CatalogVersion={catalog.CatalogVersion}; OperationCount={catalog.Operations.Count}",
                success: true,
                message: "A műveletkatalógus lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(catalog);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("OperationCatalogEndpoints")
                .LogError(exception, "A műveletkatalógus lekérdezése sikertelen.");

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "OperationCatalog",
                objectId: null,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A műveletkatalógus lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A műveletkatalógus lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}