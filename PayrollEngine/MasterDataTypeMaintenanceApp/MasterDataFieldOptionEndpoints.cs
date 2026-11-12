using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public static class MasterDataFieldOptionEndpoints
{
    public static WebApplication MapMasterDataFieldOptionEndpoints(
        this WebApplication app)
    {
        app.MapGet(
                "/api/master-data-fields/{fieldId:int}/options",
                SearchAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("SearchMasterDataFieldOptions");

        app.MapPost(
                "/api/master-data-fields/{fieldId:int}/options",
                CreateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("CreateMasterDataFieldOption");

        app.MapGet(
                "/api/master-data-field-options/{optionId:int}",
                GetByIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("GetMasterDataFieldOption");

        app.MapPut(
                "/api/master-data-field-options/{optionId:int}",
                UpdateAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_TYPE_MAINTENANCE"))
            .WithName("UpdateMasterDataFieldOption");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        int fieldId,
        string? search,
        bool? isActive,
        MasterDataFieldOptionRepository options,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataFieldOptionEndpoints");

        if (fieldId <= 0)
        {
            const string message = "Érvénytelen mezőazonosító.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);
        }

        try
        {
            var result = await options.SearchAsync(
                fieldId,
                search,
                isActive,
                cancellationToken);

            return await AuditedAsync(
                Results.Ok(result),
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(result),
                success: true,
                message: "A mező listaértékeinek lekérdezése sikeres.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 50100 or 50101)
        {
            const string message =
                "A mező nem található vagy nem lista típusú.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
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
                "Hiba történt a mező listaértékeinek lekérdezésekor. FieldId: {FieldId}",
                fieldId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A listaértékek lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "SEARCH",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A listaértékek lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> CreateAsync(
        int fieldId,
        CreateMasterDataFieldOptionRequest request,
        MasterDataFieldOptionRepository options,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataFieldOptionEndpoints");

        var validationError = ValidateRequest(
            fieldId,
            request.OptionCode,
            request.DisplayName,
            request.SortOrder);

        if (validationError is not null)
        {
            return await AuditedAsync(
                Results.BadRequest(new { message = validationError }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                validationError,
                cancellationToken);
        }

        try
        {
            var optionId = await options.CreateAsync(
                fieldId,
                request,
                cancellationToken);

            return await AuditedAsync(
                Results.Created(
                    $"/api/master-data-field-options/{optionId}",
                    new
                    {
                        optionId,
                        fieldId,
                        request.OptionCode,
                        request.DisplayName,
                        request.SortOrder,
                        isActive = true,
                        message = "A listaérték létrejött."
                    }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(new
                {
                    fieldId,
                    request.OptionCode,
                    request.DisplayName,
                    request.SortOrder,
                    IsActive = true
                }),
                success: true,
                message: "A listaérték létrejött.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            const string message =
                "Ez a listaelem-kód már létezik ennél a mezőnél.";

            return await AuditedAsync(
                Results.Conflict(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 50100 or 50101)
        {
            const string message =
                "A mező nem található vagy nem lista típusú.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 50102 or 50103)
        {
            const string message = "A listaérték adatai érvénytelenek.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
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
                "Hiba történt a listaérték létrehozásakor. FieldId: {FieldId}",
                fieldId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A listaérték létrehozása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "CREATE",
                objectId: fieldId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A listaérték létrehozása sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> GetByIdAsync(
        int optionId,
        MasterDataFieldOptionRepository options,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataFieldOptionEndpoints");

        if (optionId <= 0)
        {
            const string message = "Érvénytelen listaelem-azonosító.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);
        }

        try
        {
            var result = await options.GetByIdAsync(
                optionId,
                cancellationToken);

            if (result is null)
            {
                const string message = "A listaelem nem található.";

                return await AuditedAsync(
                    Results.NotFound(new { message }),
                    auditLog,
                    httpContext,
                    logger,
                    operation: "READ",
                    objectId: optionId.ToString(),
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
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(result),
                success: true,
                message: "A listaelem lekérdezése sikeres.",
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50104)
        {
            const string message = "Érvénytelen listaelem-azonosító.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50105)
        {
            const string message = "A listaelem nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: optionId.ToString(),
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
                "Hiba történt a listaelem lekérdezésekor. OptionId: {OptionId}",
                optionId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A listaelem lekérdezése sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "READ",
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: null,
                success: false,
                message: "A listaelem lekérdezése sikertelen.",
                cancellationToken);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int optionId,
        UpdateMasterDataFieldOptionRequest request,
        MasterDataFieldOptionRepository options,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            "MasterDataFieldOptionEndpoints");

        var validationError = ValidateRequest(
            optionId,
            request.OptionCode,
            request.DisplayName,
            request.SortOrder);

        if (validationError is not null)
        {
            return await AuditedAsync(
                Results.BadRequest(new { message = validationError }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                validationError,
                cancellationToken);
        }

        MasterDataFieldOptionModel? original;

        try
        {
            original = await options.GetByIdAsync(
                optionId,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50105)
        {
            const string message = "A listaelem nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
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
                "Hiba történt a listaelem módosítás előtti lekérdezésekor. OptionId: {OptionId}",
                optionId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A listaelem módosítása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A listaelem módosítása sikertelen.",
                cancellationToken);
        }

        if (original is null)
        {
            const string message = "A listaelem nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
                originalValue: null,
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }

        try
        {
            await options.UpdateAsync(
                optionId,
                request,
                cancellationToken);

            return await AuditedAsync(
                Results.Ok(new
                {
                    optionId,
                    original.FieldId,
                    request.OptionCode,
                    request.DisplayName,
                    request.SortOrder,
                    request.IsActive,
                    message = "A listaérték adatai módosultak."
                }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: true,
                message: "A listaérték adatai módosultak.",
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 2601 or 2627)
        {
            const string message =
                "Ez a listaelem-kód már létezik ennél a mezőnél.";

            return await AuditedAsync(
                Results.Conflict(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 50105)
        {
            const string message = "A listaelem nem található.";

            return await AuditedAsync(
                Results.NotFound(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message,
                cancellationToken);
        }
        catch (SqlException exception)
            when (exception.Number is 50102 or 50103 or 50104)
        {
            const string message = "A listaérték adatai érvénytelenek.";

            return await AuditedAsync(
                Results.BadRequest(new { message }),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
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
                "Hiba történt a listaelem módosításakor. OptionId: {OptionId}",
                optionId);

            return await AuditedAsync(
                Results.Problem(
                    title: "A listaelem módosítása sikertelen.",
                    statusCode: StatusCodes.Status500InternalServerError),
                auditLog,
                httpContext,
                logger,
                operation: "UPDATE",
                objectId: optionId.ToString(),
                originalValue: JsonSerializer.Serialize(original),
                updatedValue: JsonSerializer.Serialize(request),
                success: false,
                message: "A listaelem módosítása sikertelen.",
                cancellationToken);
        }
    }

    private static string? ValidateRequest(
        int id,
        string code,
        string displayName,
        int sortOrder)
    {
        if (id <= 0)
        {
            return "Érvénytelen mező- vagy listaelem-azonosító.";
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
        {
            return "A listaelem kódja nem lehet üres, és legfeljebb 50 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100)
        {
            return "A listaelem neve nem lehet üres, és legfeljebb 100 karakter lehet.";
        }

        if (sortOrder < 0)
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
                "MasterDataFieldOption",
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
                "A listaelem auditnapló-bejegyzésének mentése sikertelen. Operation: {Operation}, OptionId: {OptionId}",
                operation,
                objectId);
        }

        return result;
    }
}