using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.ReportBuilderApp;

public static class ReportDataCatalogEndpoints
{
    public static WebApplication MapReportDataCatalogEndpoints(
        this WebApplication app)
    {
        app.MapGet(
                "/api/reports/data-catalog",
                SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "REPORT_BUILDER"))
            .WithName("SearchReportDataCatalog");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        string? sourceCode,
        ReportDataCatalogService catalogService,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var selectedCode = string.IsNullOrWhiteSpace(sourceCode)
            ? "PAYROLL_RESULT"
            : sourceCode.Trim();

        var searchText = search?.Trim();

        try
        {
            var catalog = await catalogService.GetCatalogAsync(
                cancellationToken);

            var source = catalog.Sources.FirstOrDefault(item =>
                string.Equals(
                    item.Code,
                    selectedCode,
                    StringComparison.OrdinalIgnoreCase));

            if (source is null)
            {
                await auditLog.WriteAsync(
                    httpContext,
                    operation: "READ",
                    objectType: "ReportDataCatalog",
                    objectId: selectedCode,
                    originalValue: null,
                    updatedValue: $"Search={searchText}",
                    success: false,
                    message: "A riport-adatforrás nem található.",
                    cancellationToken);

                return Results.NotFound(new
                {
                    message =
                        $"A riport-adatforrás nem található: {selectedCode}"
                });
            }

            var relationLookup = source.Relations.ToDictionary(
                item => item.Code,
                StringComparer.OrdinalIgnoreCase);

            var references = source.Fields
                .Where(item =>
                    string.IsNullOrWhiteSpace(searchText) ||
                    item.DisplayName.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Reference.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) ||
                    item.Column.Contains(
                        searchText,
                        StringComparison.OrdinalIgnoreCase))
                .Select(item => new
                {
                    reference = item.Reference,
                    displayName = item.DisplayName,
                    dataType = item.Type,
                    category = item.RequiredRelationCode is null
                        ? source.DisplayName
                        : relationLookup[item.RequiredRelationCode].DisplayName,
                    isNullable = item.IsNullable,
                    canFilter = item.CanFilter,
                    canGroup = item.CanGroup,
                    canSort = item.CanSort,
                    requiredRelationCode = item.RequiredRelationCode,
                    isDynamic = item.DynamicField is not null
                })
                .OrderBy(item => item.category)
                .ThenBy(item => item.displayName)
                .ToArray();

            var sources = catalog.Sources
                .OrderBy(item => item.DisplayName)
                .Select(item => new
                {
                    code = item.Code,
                    displayName = item.DisplayName,
                    employeeIdField = item.EmployeeIdField,
                    companyIdField = item.CompanyIdField,
                    supportsEmployeeDocuments =
                        !string.IsNullOrWhiteSpace(item.EmployeeIdField)
                })
                .ToArray();

            var availableRelations = source.Relations
                .OrderBy(item => item.DisplayName)
                .Select(item => new
                {
                    code = item.Code,
                    displayName = item.DisplayName,
                    joinType = item.JoinType,
                    cardinality = item.Cardinality,
                    parentRelationCode = item.ParentRelationCode,
                    validity = item.Validity
                })
                .ToArray();

            var supportsEmployeeDocuments =
                !string.IsNullOrWhiteSpace(source.EmployeeIdField);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "ReportDataCatalog",
                objectId: source.Code,
                originalValue: null,
                updatedValue:
                    $"Search={searchText}; " +
                    $"ResultCount={references.Length}; " +
                    $"SourceCount={sources.Length}; " +
                    $"SupportsEmployeeDocuments={supportsEmployeeDocuments}",
                success: true,
                message: "A riport-adatkatalógus lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                search = searchText,
                sourceCode = source.Code,
                sourceName = source.DisplayName,
                employeeIdField = source.EmployeeIdField,
                companyIdField = source.CompanyIdField,
                supportsEmployeeDocuments,
                count = references.Length,
                references,
                sources,
                relations = availableRelations,
                validity = source.Validity
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger(
                nameof(ReportDataCatalogEndpoints));

            logger.LogError(
                exception,
                "A riport-adatkatalógus lekérdezése sikertelen. " +
                "SourceCode: {SourceCode}, Search: {Search}",
                selectedCode,
                searchText);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "ReportDataCatalog",
                objectId: selectedCode,
                originalValue: null,
                updatedValue: $"Search={searchText}",
                success: false,
                message: "A riport-adatkatalógus lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A riport-adatkatalógus lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}