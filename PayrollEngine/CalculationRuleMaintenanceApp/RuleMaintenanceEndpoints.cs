using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public static class RuleMaintenanceEndpoints
{
    public static WebApplication MapRuleMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/calculation-rules", GetAllAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim(
                    "app",
                    "CALCULATION_RULE_MAINTENANCE"))
            .WithName("GetCalculationRules");

        app.MapGet("/api/calculation-rules/callable", GetCallableRulesAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim(
                    "app",
                    "CALCULATION_RULE_MAINTENANCE"))
            .WithName("GetCallableCalculationRules");

        app.MapGet("/api/calculation-rules/{ruleCode}", GetByCodeAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim(
                    "app",
                    "CALCULATION_RULE_MAINTENANCE"))
            .WithName("GetCalculationRule");

        app.MapPost("/api/calculation-rules", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim(
                    "app",
                    "CALCULATION_RULE_MAINTENANCE"))
            .WithName("CreateCalculationRule");

        app.MapPut("/api/calculation-rules/{ruleCode}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim(
                    "app",
                    "CALCULATION_RULE_MAINTENANCE"))
            .WithName("UpdateCalculationRule");

        app.MapDelete("/api/calculation-rules/{ruleCode}", DeleteAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim(
                    "app",
                    "CALCULATION_RULE_MAINTENANCE"))
            .WithName("DeleteCalculationRule");

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        RuleFileStorageService rules,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await rules.GetAllAsync(cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                ruleCode: null,
                updatedValue: $"RuleCount={result.Count}",
                success: true,
                message: "A számítási szabályok lekérdezése sikerült.",
                cancellationToken: cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("RuleMaintenanceEndpoints")
                .LogError(
                    exception,
                    "A számítási szabályok lekérdezése sikertelen.");

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                ruleCode: null,
                updatedValue: null,
                success: false,
                message: "A számítási szabályok lekérdezése sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A számítási szabályok lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetCallableRulesAsync(
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
                .Where(rule => string.Equals(
                    rule.RuleType,
                    "callable",
                    StringComparison.OrdinalIgnoreCase) && rule.IsActive)
                .Select(rule => new
                {
                    rule.RuleCode,
                    rule.Name,
                    rule.ReturnType,
                    rule.Parameters,
                    rule.Version,
                    rule.IsActive
                })
                .OrderBy(rule => rule.Name)
                .ToArray();

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                ruleCode: null,
                updatedValue: $"CallableRuleCount={result.Length}",
                success: true,
                message: "A meghívható segédszabályok lekérdezése sikerült.",
                cancellationToken: cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("RuleMaintenanceEndpoints")
                .LogError(
                    exception,
                    "A meghívható segédszabályok lekérdezése sikertelen.");

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                ruleCode: null,
                updatedValue: null,
                success: false,
                message: "A meghívható segédszabályok lekérdezése sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A meghívható segédszabályok lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetByCodeAsync(
        string ruleCode,
        RuleFileStorageService rules,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await rules.GetByCodeAsync(
                ruleCode,
                cancellationToken);

            if (rule is null)
            {
                await WriteAuditAsync(
                    httpContext,
                    auditLog,
                    operation: "READ",
                    ruleCode: ruleCode,
                    updatedValue: null,
                    success: false,
                    message: "A számítási szabály nem található.",
                    cancellationToken: cancellationToken);

                return Results.NotFound(new
                {
                    message = "A számítási szabály nem található."
                });
            }

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                ruleCode: ruleCode,
                updatedValue: $"Version={rule.Version}; IsActive={rule.IsActive}",
                success: true,
                message: "A számítási szabály lekérdezése sikerült.",
                cancellationToken: cancellationToken);

            return Results.Ok(rule);
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                ruleCode: ruleCode,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken: cancellationToken);

            return Results.BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("RuleMaintenanceEndpoints")
                .LogError(
                    exception,
                    "A számítási szabály lekérdezése sikertelen. RuleCode: {RuleCode}",
                    ruleCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "READ",
                ruleCode: ruleCode,
                updatedValue: null,
                success: false,
                message: "A számítási szabály lekérdezése sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A számítási szabály lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateAsync(
        CalculationRuleDefinition request,
        RuleFileStorageService rules,
        RuleDefinitionValidator validator,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validationErrors = await validator.ValidateAsync(
            request,
            cancellationToken);

        if (validationErrors.Count > 0)
        {
            var validationMessage = string.Join(" ", validationErrors);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                ruleCode: request.RuleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: validationMessage,
                cancellationToken: cancellationToken);

            return Results.BadRequest(new
            {
                message = "A szabály adatai hibásak.",
                errors = validationErrors
            });
        }

        try
        {
            await rules.CreateAsync(request, cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                ruleCode: request.RuleCode,
                updatedValue: GetRuleSummary(request),
                success: true,
                message: "A számítási szabály létrejött.",
                cancellationToken: cancellationToken);

            return Results.Created(
                $"/api/calculation-rules/{request.RuleCode}",
                request);
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                ruleCode: request.RuleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: exception.Message,
                cancellationToken: cancellationToken);

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
                ruleCode: request.RuleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: exception.Message,
                cancellationToken: cancellationToken);

            return Results.Conflict(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("RuleMaintenanceEndpoints")
                .LogError(
                    exception,
                    "A számítási szabály létrehozása sikertelen. RuleCode: {RuleCode}",
                    request.RuleCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "CREATE",
                ruleCode: request.RuleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: "A számítási szabály létrehozása sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A számítási szabály létrehozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAsync(
        string ruleCode,
        CalculationRuleDefinition request,
        RuleFileStorageService rules,
        RuleDefinitionValidator validator,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validationErrors = await validator.ValidateAsync(
            request,
            cancellationToken);

        if (validationErrors.Count > 0)
        {
            var validationMessage = string.Join(" ", validationErrors);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                ruleCode: ruleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: validationMessage,
                cancellationToken: cancellationToken);

            return Results.BadRequest(new
            {
                message = "A szabály adatai hibásak.",
                errors = validationErrors
            });
        }

        try
        {
            await rules.UpdateAsync(
                ruleCode,
                request,
                cancellationToken);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                ruleCode: ruleCode,
                updatedValue: GetRuleSummary(request),
                success: true,
                message: "A számítási szabály módosult.",
                cancellationToken: cancellationToken);

            return Results.Ok(new
            {
                request.RuleCode,
                request.Name,
                request.Version,
                request.IsActive,
                request.RuleType,
                request.Parameters,
                request.ReturnType,
                stepCount = request.Steps.Count,
                message = "A számítási szabály módosult."
            });
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                ruleCode: ruleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: exception.Message,
                cancellationToken: cancellationToken);

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
                ruleCode: ruleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: "A számítási szabály nem található.",
                cancellationToken: cancellationToken);

            return Results.NotFound(new
            {
                message = "A számítási szabály nem található."
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("RuleMaintenanceEndpoints")
                .LogError(
                    exception,
                    "A számítási szabály módosítása sikertelen. RuleCode: {RuleCode}",
                    ruleCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "UPDATE",
                ruleCode: ruleCode,
                updatedValue: GetRuleSummary(request),
                success: false,
                message: "A számítási szabály módosítása sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A számítási szabály módosítása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> DeleteAsync(
        string ruleCode,
        RuleFileStorageService rules,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var deletedRule = await rules.DeleteAsync(
                ruleCode,
                cancellationToken);

            if (deletedRule is null)
            {
                await WriteAuditAsync(
                    httpContext,
                    auditLog,
                    operation: "DELETE",
                    ruleCode: ruleCode,
                    updatedValue: null,
                    success: false,
                    message: "A számítási szabály nem található.",
                    cancellationToken: cancellationToken);

                return Results.NotFound(new
                {
                    message = "A számítási szabály nem található."
                });
            }

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "DELETE",
                ruleCode: ruleCode,
                updatedValue: null,
                success: true,
                message: "A számítási szabály törlése sikerült.",
                cancellationToken: cancellationToken,
                originalValue: GetRuleSummary(deletedRule));

            return Results.Ok(new
            {
                ruleCode,
                message = "A számítási szabály törlése sikerült."
            });
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "DELETE",
                ruleCode: ruleCode,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken: cancellationToken);

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
                operation: "DELETE",
                ruleCode: ruleCode,
                updatedValue: null,
                success: false,
                message: exception.Message,
                cancellationToken: cancellationToken);

            return Results.Conflict(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("RuleMaintenanceEndpoints")
                .LogError(
                    exception,
                    "A számítási szabály törlése sikertelen. RuleCode: {RuleCode}",
                    ruleCode);

            await WriteAuditAsync(
                httpContext,
                auditLog,
                operation: "DELETE",
                ruleCode: ruleCode,
                updatedValue: null,
                success: false,
                message: "A számítási szabály törlése sikertelen.",
                cancellationToken: cancellationToken);

            return Results.Problem(
                title: "A számítási szabály törlése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Task WriteAuditAsync(
        HttpContext httpContext,
        AuditLogService auditLog,
        string operation,
        string? ruleCode,
        string? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken,
        string? originalValue = null)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: operation,
            objectType: "CalculationRule",
            objectId: ruleCode,
            originalValue: originalValue,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }

    private static string GetRuleSummary(CalculationRuleDefinition rule)
    {
        var stepCount = rule.Steps?.Count ?? 0;

        return $"Name={rule.Name}; Version={rule.Version}; " +
               $"IsActive={rule.IsActive}; RuleType={rule.RuleType}; " +
               $"ParameterCount={rule.Parameters?.Count ?? 0}; " +
               $"ReturnType={rule.ReturnType}; StepCount={stepCount}";
    }
}
