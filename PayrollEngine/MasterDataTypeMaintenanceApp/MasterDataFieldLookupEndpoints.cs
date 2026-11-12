using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public static class MasterDataFieldLookupEndpoints
{
    public static WebApplication MapMasterDataFieldLookupEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/master-data-field-types", GetFieldTypesAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("GetMasterDataFieldTypes");

        app.MapGet("/api/master-data-groups", GetMasterDataGroupsAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("GetMasterDataGroups");

        return app;
    }

    private static Task<IResult> GetFieldTypesAsync(
        MasterDataFieldLookupRepository lookups,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataFieldLookupEndpoints");

        return ReadLookupAsync(
            token => lookups.GetFieldTypesAsync(token),
            "FieldType",
            "A mezőtípusok lekérdezése sikeres.",
            "A mezőtípusok lekérdezése sikertelen.",
            auditLog,
            httpContext,
            logger,
            cancellationToken);
    }

    private static Task<IResult> GetMasterDataGroupsAsync(
        MasterDataFieldLookupRepository lookups,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataFieldLookupEndpoints");

        return ReadLookupAsync(
            token => lookups.GetMasterDataGroupsAsync(token),
            "MasterDataGroup",
            "A törzsadatcsoportok lekérdezése sikeres.",
            "A törzsadatcsoportok lekérdezése sikertelen.",
            auditLog,
            httpContext,
            logger,
            cancellationToken);
    }

    private static async Task<IResult> ReadLookupAsync<T>(
        Func<CancellationToken, Task<IReadOnlyList<T>>> query,
        string objectType,
        string successMessage,
        string failureMessage,
        AuditLogService auditLog,
        HttpContext httpContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await query(cancellationToken);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                objectType,
                JsonSerializer.Serialize(result),
                success: true,
                successMessage,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a {ObjectType} választólista lekérdezésekor.",
                objectType);

            await WriteAuditSafelyAsync(
                auditLog,
                httpContext,
                logger,
                objectType,
                updatedValue: null,
                success: false,
                failureMessage,
                cancellationToken);

            return Results.Problem(
                title: failureMessage,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteAuditSafelyAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        ILogger logger,
        string objectType,
        string? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                "READ",
                objectType,
                objectId: null,
                originalValue: null,
                updatedValue,
                success,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "A választólista auditnapló-bejegyzésének mentése sikertelen. ObjectType: {ObjectType}",
                objectType);
        }
    }
}