using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class MasterDataGroupAccessEndpoints
{
    public static WebApplication MapMasterDataGroupAccessEndpoints(
        this WebApplication app)
    {
        app.MapGet(
                "/api/master-data-groups/access",
                GetAccessibleGroupsAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"))
            .WithName("GetAccessibleMasterDataGroups");

        return app;
    }

    private static async Task<IResult> GetAccessibleGroupsAsync(
        HttpContext httpContext,
        MasterDataGroupAccessRepository accessRepository,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdValue, out var userId) || userId <= 0)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "MasterDataGroupAccess",
                objectId: null,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A bejelentkezett felhasználó azonosítója hiányzik.",
                cancellationToken);

            return Results.Unauthorized();
        }

        try
        {
            var groups = await accessRepository.GetByUserIdAsync(
                userId,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "MasterDataGroupAccess",
                objectId: userId.ToString(),
                originalValue: null,
                updatedValue: $"MasterDataGroupCount={groups.Count}",
                success: true,
                message: "A törzsadatcsoport-jogosultságok lekérdezése sikerült.",
                cancellationToken);

            return Results.Ok(groups);
        }
        catch (SqlException exception) when (exception.Number == 50311)
        {
            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "MasterDataGroupAccess",
                objectId: userId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A felhasználó nem található vagy inaktív.",
                cancellationToken);

            return Results.Json(
                new { message = "A felhasználó nem található vagy inaktív." },
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger(
                "MasterDataGroupAccessEndpoints");

            logger.LogError(
                exception,
                "Hiba történt a törzsadatcsoport-jogosultságok lekérdezésekor. UserId: {UserId}",
                userId);

            await auditLog.WriteAsync(
                httpContext,
                operation: "READ",
                objectType: "MasterDataGroupAccess",
                objectId: userId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A törzsadatcsoport-jogosultságok lekérdezése sikertelen.",
                cancellationToken);

            return Results.Problem(
                title: "A törzsadatcsoport-jogosultságok lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}