using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public static class ReportDefinitionEndpoints
{
    public static WebApplication MapReportDefinitionEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/reports", GetAllAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "REPORT_BUILDER"))
            .WithName("GetReportDefinitions");

        app.MapGet("/api/reports/{reportCode}", GetByCodeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "REPORT_BUILDER"))
            .WithName("GetReportDefinition");

        app.MapPost("/api/reports", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "REPORT_BUILDER"))
            .WithName("CreateReportDefinition");

        app.MapPut("/api/reports/{reportCode}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "REPORT_BUILDER"))
            .WithName("UpdateReportDefinition");

        app.MapDelete("/api/reports/{reportCode}", DeleteAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "REPORT_BUILDER"))
            .WithName("DeleteReportDefinition");

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        ReportDefinitionFileStorageService reports,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await reports.GetAllAsync(cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "ReportDefinition",
                objectId: null,
                originalValue: null,
                updatedValue: $"ReportCount={result.Count}",
                success: true,
                message: "A riportdefiníciók lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("ReportDefinitionEndpoints");
            logger.LogError(exception, "Hiba történt a riportdefiníciók lekérdezésekor.");

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "ReportDefinition",
                objectId: null,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A riportdefiníciók lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A riportdefiníciók lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetByCodeAsync(
        string reportCode,
        ReportDefinitionFileStorageService reports,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await reports.GetByCodeAsync(
                reportCode,
                cancellationToken);

            if (result is null)
            {
                await auditLog.WriteAsync(
                    httpContext,
                    operation: "READ",
                    objectType: "ReportDefinition",
                    objectId: reportCode,
                    originalValue: null,
                    updatedValue: null,
                    success: false,
                    message: "A riportdefiníció nem található.",
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = "A riportdefiníció nem található."
                });
            }

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: $"Name={result.Name}",
                success: true,
                message: "A riportdefiníció lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("ReportDefinitionEndpoints");
            logger.LogError(
                exception,
                "Hiba történt a riportdefiníció lekérdezésekor. ReportCode: {ReportCode}",
                reportCode);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A riportdefiníció lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A riportdefiníció lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateAsync(
        ReportDefinition request,
        ReportDefinitionFileStorageService reports,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await reports.CreateAsync(request, cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "ReportDefinition",
                objectId: request.ReportCode,
                originalValue: null,
                updatedValue: $"Name={request.Name}; DataSource={request.DataSource}",
                success: true,
                message: "A riportdefiníció létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/reports/{request.ReportCode}",
                new
                {
                    request.ReportCode,
                    request.Name,
                    request.Version,
                    request.IsActive,
                    message = "A riportdefiníció létrejött."
                });
        }
        catch (ArgumentException exception)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "ReportDefinition",
                objectId: request.ReportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "ReportDefinition",
                objectId: request.ReportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.Conflict(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("ReportDefinitionEndpoints");
            logger.LogError(
                exception,
                "Hiba történt a riportdefiníció létrehozásakor. ReportCode: {ReportCode}",
                request.ReportCode);

            await auditLog.WriteAsync(
                httpContext,
                operation: "CREATE",
                objectType: "ReportDefinition",
                objectId: request.ReportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A riportdefiníció létrehozása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A riportdefiníció létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAsync(
        string reportCode,
        ReportDefinition request,
        ReportDefinitionFileStorageService reports,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await reports.GetByCodeAsync(
                reportCode,
                cancellationToken);

            if (existing is null)
            {
                await auditLog.WriteAsync(
                    httpContext,
                    operation: "UPDATE",
                    objectType: "ReportDefinition",
                    objectId: reportCode,
                    originalValue: null,
                    updatedValue: null,
                    success: false,
                    message: "A riportdefiníció nem található.",
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = "A riportdefiníció nem található."
                });
            }

            await reports.UpdateAsync(
                reportCode,
                request,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: $"Name={existing.Name}; DataSource={existing.DataSource}",
                updatedValue: $"Name={request.Name}; DataSource={request.DataSource}",
                success: true,
                message: "A riportdefiníció módosult.",
                cancellationToken);

            return Results.Ok(new
            {
                request.ReportCode,
                request.Name,
                request.Version,
                request.IsActive,
                message = "A riportdefiníció módosult."
            });
        }
        catch (ArgumentException exception)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.BadRequest(new { message = exception.Message });
        }
        catch (FileNotFoundException)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A riportdefiníció nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A riportdefiníció nem található."
            });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("ReportDefinitionEndpoints");
            logger.LogError(
                exception,
                "Hiba történt a riportdefiníció módosításakor. ReportCode: {ReportCode}",
                reportCode);

            await auditLog.WriteAsync(
                httpContext,
                operation: "UPDATE",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A riportdefiníció módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A riportdefiníció módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> DeleteAsync(
        string reportCode,
        ReportDefinitionFileStorageService reports,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await reports.DeleteAsync(
                reportCode,
                cancellationToken);

            if (deleted is null)
            {
                await auditLog.WriteAsync(
                    httpContext,
                    operation: "DELETE",
                    objectType: "ReportDefinition",
                    objectId: reportCode,
                    originalValue: null,
                    updatedValue: null,
                    success: false,
                    message: "A riportdefiníció nem található.",
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = "A riportdefiníció nem található."
                });
            }

            await auditLog.WriteAsync(
                httpContext,
                operation: "DELETE",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: $"Name={deleted.Name}; DataSource={deleted.DataSource}",
                updatedValue: null,
                success: true,
                message: "A riportdefiníció törlése sikerült.",
                cancellationToken);

            return Results.Ok(new
            {
                reportCode,
                message = "A riportdefiníció törlése sikerült."
            });
        }
        catch (ArgumentException exception)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "DELETE",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger("ReportDefinitionEndpoints");
            logger.LogError(
                exception,
                "Hiba történt a riportdefiníció törlésekor. ReportCode: {ReportCode}",
                reportCode);

            await auditLog.WriteAsync(
                httpContext,
                operation: "DELETE",
                objectType: "ReportDefinition",
                objectId: reportCode,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A riportdefiníció törlése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A riportdefiníció törlése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}