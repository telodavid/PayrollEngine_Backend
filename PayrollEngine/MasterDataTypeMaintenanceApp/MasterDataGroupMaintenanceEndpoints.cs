using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public static class MasterDataGroupMaintenanceEndpoints
{
    public static WebApplication MapMasterDataGroupMaintenanceEndpoints(
        this WebApplication app)
    {
        app.MapPost("/api/master-data-groups", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("CreateMasterDataGroup");

        app.MapPut("/api/master-data-groups/{masterDataGroupId:int}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("UpdateMasterDataGroup");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        SaveMasterDataGroupRequest request,
        MasterDataGroupMaintenanceRepository groups,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataGroupMaintenanceEndpoints");

        var validationError = Validate(request);

        if (validationError is not null)
        {
            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "CREATE", null, request, false, validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var id = await groups.CreateAsync(
                request with
                {
                    Code = request.Code.Trim(),
                    Name = request.Name.Trim()
                },
                cancellationToken);

            const string message = "A törzsadatcsoport létrejött.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "CREATE", id.ToString(), request, true, message,
                cancellationToken);

            return Results.Created(
                $"/api/master-data-groups/{id}",
                new
                {
                    masterDataGroupId = id,
                    code = request.Code.Trim(),
                    name = request.Name.Trim(),
                    message
                });
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            const string message = "Ez a csoportkód már használatban van.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "CREATE", null, request, false, message,
                cancellationToken);

            return Results.Conflict(new { message });
        }
        catch (SqlException exception) when (exception.Number == 51101)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (SqlException exception) when (exception.Number == 51102)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Hiba történt a törzsadatcsoport létrehozásakor.");

            const string message = "A törzsadatcsoport létrehozása sikertelen.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "CREATE", null, request, false, message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int masterDataGroupId,
        SaveMasterDataGroupRequest request,
        MasterDataGroupMaintenanceRepository groups,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataGroupMaintenanceEndpoints");

        if (masterDataGroupId <= 0)
        {
            const string message = "Érvénytelen törzsadatcsoport-azonosító.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "UPDATE", masterDataGroupId.ToString(), request, false, message,
                cancellationToken);

            return Results.BadRequest(new { message });
        }

        var validationError = Validate(request);

        if (validationError is not null)
        {
            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "UPDATE", masterDataGroupId.ToString(), request, false, validationError,
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            await groups.UpdateAsync(
                masterDataGroupId,
                request with
                {
                    Code = request.Code.Trim(),
                    Name = request.Name.Trim()
                },
                cancellationToken);

            const string message = "A törzsadatcsoport adatai módosultak.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "UPDATE", masterDataGroupId.ToString(), request, true, message,
                cancellationToken);

            return Results.Ok(new
            {
                masterDataGroupId,
                code = request.Code.Trim(),
                name = request.Name.Trim(),
                message
            });
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            const string message = "Ez a csoportkód már használatban van.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "UPDATE", masterDataGroupId.ToString(), request, false, message,
                cancellationToken);

            return Results.Conflict(new { message });
        }
        catch (SqlException exception) when (exception.Number == 51104)
        {
            const string message = "A törzsadatcsoport nem található.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "UPDATE", masterDataGroupId.ToString(), request, false, message,
                cancellationToken);

            return Results.NotFound(new { message });
        }
        catch (SqlException exception) when (exception.Number is 51101 or 51102 or 51103)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a törzsadatcsoport módosításakor. Azonosító: {Id}",
                masterDataGroupId);

            const string message = "A törzsadatcsoport módosítása sikertelen.";

            await WriteAuditSafelyAsync(
                auditLog, httpContext, logger,
                "UPDATE", masterDataGroupId.ToString(), request, false, message,
                cancellationToken);

            return Results.Problem(
                title: message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? Validate(SaveMasterDataGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 50)
            return "A csoportkód nem lehet üres, és legfeljebb 50 karakter lehet.";

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            return "A csoport neve nem lehet üres, és legfeljebb 100 karakter lehet.";

        return null;
    }

    private static async Task WriteAuditSafelyAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        ILogger logger,
        string operation,
        string? objectId,
        SaveMasterDataGroupRequest request,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: operation,
                objectType: "MasterDataGroup",
                objectId: objectId,
                originalValue: null,
                updatedValue: $"Code={request.Code}; Name={request.Name}",
                success: success,
                message: message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "A törzsadatcsoport auditnaplózása sikertelen.");
        }
    }
}