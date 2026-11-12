using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.CalculationRuleMaintenanceApp;
using PayrollEngine.PayrollSchemaMaintenanceApp.Models;

namespace PayrollEngine.PayrollSchemaMaintenanceApp;

public static class PayrollSchemaEndpoints
{
    public static WebApplication MapPayrollSchemaEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/payroll-schemas", GetAllAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_SCHEMA_MAINTENANCE"))
            .WithName("GetPayrollSchemas");

        app.MapGet(
                "/api/payroll-schemas/available-rules",
                GetAvailableRulesAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_SCHEMA_MAINTENANCE"))
            .WithName("GetRulesAvailableForPayrollSchemas");

        app.MapGet(
                "/api/payroll-schemas/{schemaCode}",
                GetByCodeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_SCHEMA_MAINTENANCE"))
            .WithName("GetPayrollSchema");

        app.MapPost("/api/payroll-schemas", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_SCHEMA_MAINTENANCE"))
            .WithName("CreatePayrollSchema");

        app.MapPut(
                "/api/payroll-schemas/{schemaCode}",
                UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_SCHEMA_MAINTENANCE"))
            .WithName("UpdatePayrollSchema");

        app.MapDelete(
                "/api/payroll-schemas/{schemaCode}",
                DeleteAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_SCHEMA_MAINTENANCE"))
            .WithName("DeletePayrollSchema");

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        PayrollSchemaFileStorageService schemas,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await schemas.GetAllAsync(cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                objectType: "PayrollSchema",
                objectId: null,
                updatedValue: $"SchemaCount={result.Count}",
                success: true,
                message: "A számfejtési sémák lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollSchemaEndpoints")
                .LogError(exception, "A sémák lekérdezése sikertelen.");

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                objectType: "PayrollSchema",
                objectId: null,
                updatedValue: null,
                success: false,
                message: "A számfejtési sémák lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési sémák lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetAvailableRulesAsync(
        RuleFileStorageService rules,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var availableRules = await rules.GetAllAsync(cancellationToken);

            var result = availableRules
                .Select(rule => new
                {
                    rule.RuleCode,
                    rule.Name,
                    rule.Version,
                    rule.IsActive
                })
                .OrderBy(rule => rule.Name)
                .ToArray();

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                objectType: "PayrollSchemaRuleCatalog",
                objectId: null,
                updatedValue: $"RuleCount={result.Length}",
                success: true,
                message: "A sémához választható szabályok lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollSchemaEndpoints")
                .LogError(
                    exception,
                    "A sémához választható szabályok lekérdezése sikertelen.");

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                objectType: "PayrollSchemaRuleCatalog",
                objectId: null,
                updatedValue: null,
                success: false,
                message: "A választható szabályok lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A választható szabályok lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetByCodeAsync(
        string schemaCode,
        PayrollSchemaFileStorageService schemas,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var schema = await schemas.GetByCodeAsync(
                schemaCode,
                cancellationToken);

            if (schema is null)
            {
                await WriteAuditAsync(
                    httpContext,
                    auditLog,
                    operation: "READ",
                    objectType: "PayrollSchema",
                    objectId: schemaCode,
                    updatedValue: null,
                    success: false,
                    message: "A számfejtési séma nem található.",
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = "A számfejtési séma nem található."
                });
            }

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: GetSchemaSummary(schema),
                success: true,
                message: "A számfejtési séma lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(schema);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollSchemaEndpoints")
                .LogError(
                    exception,
                    "A séma lekérdezése sikertelen. SchemaCode: {SchemaCode}",
                    schemaCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: null,
                success: false,
                message: "A számfejtési séma lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési séma lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateAsync(
        PayrollSchemaDefinition request,
        PayrollSchemaFileStorageService schemas,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await schemas.CreateAsync(request, cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                objectType: "PayrollSchema",
                objectId: request.SchemaCode,
                updatedValue: GetSchemaSummary(request),
                success: true,
                message: "A számfejtési séma létrejött.",
                cancellationToken);

            return Results.Created(
                $"/api/payroll-schemas/{request.SchemaCode}",
                request);
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                objectType: "PayrollSchema",
                objectId: request.SchemaCode,
                updatedValue: GetSchemaSummary(request),
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                objectType: "PayrollSchema",
                objectId: request.SchemaCode,
                updatedValue: GetSchemaSummary(request),
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.Conflict(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollSchemaEndpoints")
                .LogError(
                    exception,
                    "A séma létrehozása sikertelen. SchemaCode: {SchemaCode}",
                    request.SchemaCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                objectType: "PayrollSchema",
                objectId: request.SchemaCode,
                updatedValue: GetSchemaSummary(request),
                success: false,
                message: "A számfejtési séma létrehozása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési séma létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAsync(
        string schemaCode,
        PayrollSchemaDefinition request,
        PayrollSchemaFileStorageService schemas,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await schemas.UpdateAsync(
                schemaCode,
                request,
                cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: GetSchemaSummary(request),
                success: true,
                message: "A számfejtési séma módosult.",
                cancellationToken);

            return Results.Ok(new
            {
                request.SchemaCode,
                request.Name,
                request.Version,
                request.IsActive,
                blockCount = request.Blocks?.Count ?? 0,
                ruleCount = request.Blocks?.Sum(
                    block => block.Rules?.Count ?? 0) ?? 0,
                message = "A számfejtési séma módosult."
            });
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: GetSchemaSummary(request),
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (FileNotFoundException)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: GetSchemaSummary(request),
                success: false,
                message: "A számfejtési séma nem található.",
                cancellationToken);

            return Results.NotFound(new
            {
                message = "A számfejtési séma nem található."
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollSchemaEndpoints")
                .LogError(
                    exception,
                    "A séma módosítása sikertelen. SchemaCode: {SchemaCode}",
                    schemaCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: GetSchemaSummary(request),
                success: false,
                message: "A számfejtési séma módosítása sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési séma módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> DeleteAsync(
        string schemaCode,
        PayrollSchemaFileStorageService schemas,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedSchema = await schemas.DeleteAsync(
                schemaCode,
                cancellationToken);

            if (deletedSchema is null)
            {
                await WriteAuditAsync(
                    httpContext,
                    auditLog,
                    operation: "DELETE",
                    objectType: "PayrollSchema",
                    objectId: schemaCode,
                    updatedValue: null,
                    success: false,
                    message: "A számfejtési séma nem található.",
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = "A számfejtési séma nem található."
                });
            }

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "DELETE",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: null,
                success: true,
                message: "A számfejtési séma törlése sikerült.",
                cancellationToken,
                originalValue: GetSchemaSummary(deletedSchema));

            return Results.Ok(new
            {
                schemaCode,
                message = "A számfejtési séma törlése sikerült."
            });
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "DELETE",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken);

            return Results.BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollSchemaEndpoints")
                .LogError(
                    exception,
                    "A séma törlése sikertelen. SchemaCode: {SchemaCode}",
                    schemaCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "DELETE",
                objectType: "PayrollSchema",
                objectId: schemaCode,
                updatedValue: null,
                success: false,
                message: "A számfejtési séma törlése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési séma törlése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Task WriteAuditAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        string operation,
        string objectType,
        string? objectId,
        string? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken,
        string? originalValue = null)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: operation,
            objectType: objectType,
            objectId: objectId,
            originalValue: originalValue,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }

    private static string GetSchemaSummary(
        PayrollSchemaDefinition schema)
    {
        var blocks = schema.Blocks ?? [];
        var ruleCount = blocks.Sum(block => block.Rules?.Count ?? 0);

        return $"Name={schema.Name}; Version={schema.Version}; " +
               $"IsActive={schema.IsActive}; BlockCount={blocks.Count}; " +
               $"RuleCount={ruleCount}";
    }
}