using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public static class DatabaseSchemaCatalogEndpoints
{
    public static WebApplication MapDatabaseSchemaCatalogEndpoints(
        this WebApplication app)
    {
        app.MapGet(
                "/api/calculation-rules/data-catalog",
                SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim(
                    "app",
                    "CALCULATION_RULE_MAINTENANCE"))
            .WithName("SearchCalculationRuleDataCatalog");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        PayrollDataCatalogService catalog,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var references = await catalog.SearchAsync(
                search,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollDataCatalog",
                objectId: null,
                originalValue: null,
                updatedValue:
                    $"Search={search ?? "*"}; ResultCount={references.Count}",
                success: true,
                message: "A számfejtési adat-katalógus lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                search,
                count = references.Count,
                references
            });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger(
                "DatabaseSchemaCatalogEndpoints");

            logger.LogError(
                exception,
                "Hiba történt a számfejtési adat-katalógus lekérdezésekor. Search: {Search}",
                search);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "PayrollDataCatalog",
                objectId: null,
                originalValue: null,
                updatedValue: $"Search={search ?? "*"}",
                success: false,
                message: "A számfejtési adat-katalógus lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "Az adat-katalógus lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}