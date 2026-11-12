using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public static class MasterDataFieldEndpoints
{
    public static WebApplication MapMasterDataFieldEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/master-data-fields", SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("SearchMasterDataFields");

        app.MapGet("/api/master-data-fields/{fieldId:int}", GetByIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("GetMasterDataField");

        app.MapPost("/api/master-data-fields", CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("CreateMasterDataField");

        app.MapPut("/api/master-data-fields/{fieldId:int}", UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("UpdateMasterDataField");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        int? masterDataGroupId,
        int? fieldTypeId,
        bool? isActive,
        string? search,
        MasterDataFieldRepository fields,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MasterDataFieldEndpoints");

        try
        {
            var result = await fields.SearchAsync(
                masterDataGroupId,
                fieldTypeId,
                isActive,
                search,
                cancellationToken);

            return await AuditedAsync(
                Results.Ok(result),
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(result),
                success: true,
                message: "A dinamikus mezők lekérdezése sikeres.",
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Hiba történt a dinamikus mezők lekérdezésekor.");

            return await AuditedAsync(
                Results.Problem(
                    title: "A dinamikus mezők lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
                objectId: null,
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A dinamikus mezők lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> GetByIdAsync(
        int fieldId,
        MasterDataFieldRepository fields,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MasterDataFieldEndpoints");

        if (fieldId <= 0)
        {
            const string message = "Érvénytelen mezőazonosító.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);
        }

        try
        {
            var result = await fields.GetByIdAsync(fieldId, cancellationToken);

            if (result is null)
            {
                const string message = "A dinamikus mező nem található.";

                return await AuditedAsync(
                    Results.NotFound(new { message }),
                    auditLog,
                    httpContext,
                    logger,
                    operation: "READ",
                    objectId: fieldId.ToString(),
                    originalValue: null,
                    updatedValue: null,
                    success: false,
                    message,
                    cancellationToken);
            }

            return await AuditedAsync(
                Results.Ok(result),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(result),
                success: true,
                message: "A dinamikus mező lekérdezése sikeres.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50090)
        {
            const string message = "Érvénytelen mezőazonosító.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50094)
        {
            const string message = "A dinamikus mező nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a dinamikus mező lekérdezésekor. FieldId: {FieldId}",
                fieldId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A dinamikus mező lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A dinamikus mező lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> CreateAsync(
        CreateMasterDataFieldRequest request,
        MasterDataFieldRepository fields,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MasterDataFieldEndpoints");
        var validationError = ValidateRequest(
            request.MasterDataGroupId,
            request.Code,
            request.DisplayName,
            request.FieldTypeId,
            request.DisplayOrder);

        if (validationError is not null)
        {
            return await AuditedAsync(
                Results.BadRequest(new { message = validationError }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                validationError,
                cancellationToken);
        }

        try
        {
            var fieldId = await fields.CreateAsync(request, cancellationToken);

            return await AuditedAsync(
                Results.Created(
                    $"/api/master-data-fields/{fieldId}",
                    new
                    {
                        fieldId,
                        request.MasterDataGroupId,
                        request.Code,
                        request.DisplayName,
                        request.FieldTypeId,
                        request.IsRequired,
                        request.IsUnique,
                        request.DisplayOrder,
                        isActive = true,
                        message = "A dinamikus mező létrejött."
                    }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: true,
                message: "A dinamikus mező létrejött.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            const string message =
                "Ez a mezőkód már használatban van ebben a törzsadatcsoportban.";

            return await AuditedAsync(
                Results.Conflict(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50091)
        {
            const string message = "A törzsadatcsoport nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50092)
        {
            const string message = "A mezőtípus nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 50090 or 50093)
        {
            const string message = "A dinamikus mező adatai érvénytelenek.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a dinamikus mező létrehozásakor. Code: {Code}",
                request.Code);

            return await AuditedAsync(
                Results.Problem(
                    title: "A dinamikus mező létrehozása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: null,
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A dinamikus mező létrehozása sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int fieldId,
        UpdateMasterDataFieldRequest request,
        MasterDataFieldRepository fields,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("MasterDataFieldEndpoints");

        if (fieldId <= 0)
        {
            const string message = "Érvénytelen mezőazonosító.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }

        var validationError = ValidateRequest(
            request.MasterDataGroupId,
            request.Code,
            request.DisplayName,
            request.FieldTypeId,
            request.DisplayOrder);

        if (validationError is not null)
        {
            return await AuditedAsync(
                Results.BadRequest(new { message = validationError }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                validationError,
                cancellationToken);
        }

        MasterDataFieldModel? original;

        try
        {
            original = await fields.GetByIdAsync(fieldId, cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50094)
        {
            const string message = "A dinamikus mező nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a dinamikus mező módosítás előtti lekérdezésekor. FieldId: {FieldId}",
                fieldId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A dinamikus mező módosítása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A dinamikus mező módosítása sikertelen.",
                cancellationToken);
        }

        if (original is null)
        {
            const string message = "A dinamikus mező nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }

        try
        {
            await fields.UpdateAsync(fieldId, request, cancellationToken);

            return await AuditedAsync(
                Results.Ok(new
                {
                    fieldId,
                    request.MasterDataGroupId,
                    request.Code,
                    request.DisplayName,
                    request.FieldTypeId,
                    request.IsRequired,
                    request.IsUnique,
                    request.DisplayOrder,
                    request.IsActive,
                    message = "A dinamikus mező adatai módosultak."
                }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: true,
                message: "A dinamikus mező adatai módosultak.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            const string message =
                "Ez a mezőkód már használatban van ebben a törzsadatcsoportban.";

            return await AuditedAsync(
                Results.Conflict(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50091)
        {
            const string message = "A törzsadatcsoport nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50092)
        {
            const string message = "A mezőtípus nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 50090 or 50093 or 50094)
        {
            const string message =
                "A dinamikus mező adatai érvénytelenek, vagy a mező nem található.";

            return await AuditedAsync(
                exception.Number == 50094
                    ? Results.NotFound(new { message })
                    : Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a dinamikus mező módosításakor. FieldId: {FieldId}",
                fieldId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A dinamikus mező módosítása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: fieldId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A dinamikus mező módosítása sikertelen.",
                cancellationToken);
        }
    }

    private static string? ValidateRequest(
        int masterDataGroupId,
        string code,
        string displayName,
        int fieldTypeId,
        int displayOrder)
    {
        if (masterDataGroupId <= 0)
        {
            return "Érvénytelen törzsadatcsoport-azonosító.";
        }

        if (fieldTypeId <= 0)
        {
            return "Érvénytelen mezőtípus-azonosító.";
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
        {
            return "A mezőkód nem lehet üres, és legfeljebb 50 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100)
        {
            return "A mező megjelenített neve nem lehet üres, és legfeljebb 100 karakter lehet.";
        }

        if (displayOrder < 0)
        {
            return "A megjelenítési sorrend nem lehet negatív.";
        }

        return null;
    }

    private static async Task<IResult> AuditedAsync(
        IResult result,
        AuditLogService auditLog,
        HttpContext httpContext,
        ILogger logger,
        string operation,
        string? objectId,
        string? originalValue,
        string? updatedValue,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.WriteAsync(
                httpContext,
                operation,
                "MasterDataField",
                objectId,
                originalValue,
                updatedValue,
                success,
                message,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "A dinamikus mező auditnapló-bejegyzésének mentése sikertelen. Operation: {Operation}, FieldId: {FieldId}",
                operation,
                objectId);
        }

        return result;
    }
}