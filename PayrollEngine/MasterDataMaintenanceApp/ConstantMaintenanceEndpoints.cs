using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class ConstantMaintenanceEndpoints
{
    public static WebApplication MapConstantMaintenanceEndpoints(
        this WebApplication app)
    {
        var endpoints = app
            .MapGroup("/api/constants")
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"));

        endpoints.MapGet("", GetAllAsync);
        endpoints.MapPost("", CreateAsync);
        endpoints.MapPut("/{constantId}/{validFrom}", UpdateAsync);
        endpoints.MapDelete("/{constantId}/{validFrom}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        ConstantMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(
                await repository.GetAllAsync(cancellationToken));
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static async Task<IResult> CreateAsync(
        CreateConstantRequest request,
        ConstantMaintenanceRepository repository,
        AuditLogService auditLog,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = Validate(
            request.ConstantId,
            request.ConstantName,
            request.Value,
            request.ValidFrom,
            request.ValidTo);

        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        request = request with
        {
            ConstantId = request.ConstantId.Trim(),
            ConstantName = request.ConstantName.Trim()
        };

        try
        {
            var result = await repository.CreateAsync(
                request,
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                "CREATE_CONSTANT",
                result,
                "A konstans rögzítése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static async Task<IResult> UpdateAsync(
        string constantId,
        DateOnly validFrom,
        UpdateConstantRequest request,
        ConstantMaintenanceRepository repository,
        AuditLogService auditLog,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = Validate(
            constantId,
            request.ConstantName,
            request.Value,
            validFrom,
            request.ValidTo);

        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        request = request with
        {
            ConstantName = request.ConstantName.Trim()
        };

        try
        {
            var result = await repository.UpdateAsync(
                constantId.Trim(),
                validFrom,
                request,
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                "UPDATE_CONSTANT",
                result,
                "A konstans módosítása sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static async Task<IResult> DeleteAsync(
        string constantId,
        DateOnly validFrom,
        ConstantMaintenanceRepository repository,
        AuditLogService auditLog,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(constantId)
            || constantId.Trim().Length > 5
            || validFrom == default)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen konstansazonosító vagy kezdődátum."
            });
        }

        try
        {
            await repository.DeleteAsync(
                constantId.Trim(),
                validFrom,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "DELETE_CONSTANT",
                objectType: "Constant",
                objectId: $"{constantId.Trim()}/{validFrom:yyyy-MM-dd}",
                originalValue: null,
                updatedValue: null,
                success: true,
                message: "A konstans törlése sikerült.",
                cancellationToken: cancellationToken);

            return Results.Ok(new
            {
                message = "A konstans törlése sikerült."
            });
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static string? Validate(
        string? constantId,
        string? constantName,
        decimal value,
        DateOnly validFrom,
        DateOnly? validTo)
    {
        if (string.IsNullOrWhiteSpace(constantId)
            || constantId.Trim().Length > 5)
        {
            return "Az azonosító megadása kötelező, és legfeljebb 5 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(constantName)
            || constantName.Trim().Length > 50)
        {
            return "A név megadása kötelező, és legfeljebb 50 karakter lehet.";
        }

        if (value != decimal.Round(value, 6)
            || value > 999999999999.999999m
            || value < -999999999999.999999m)
        {
            return "Az érték legfeljebb 18 számjegyű és 6 tizedesjegyű lehet.";
        }

        if (validFrom == default)
        {
            return "Az érvényesség kezdetének megadása kötelező.";
        }

        if (validTo.HasValue && validTo.Value < validFrom)
        {
            return "Az érvényesség vége nem lehet korábbi a kezdeténél.";
        }

        return null;
    }

    private static IResult DatabaseError(SqlException exception)
        => exception.Number switch
        {
            50801 or 50804 => Results.BadRequest(new
            {
                message = exception.Message
            }),
            50802 => Results.NotFound(new
            {
                message = exception.Message
            }),
            2601 or 2627 => Results.Conflict(new
            {
                message = "Ezzel az azonosítóval és kezdődátummal már létezik konstans."
            }),
            _ => Results.Problem(
                title: "A konstans kezelése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError)
        };

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string operation,
        ConstantItem item,
        string message,
        CancellationToken cancellationToken)
        => auditLog.WriteAsync(
            httpContext,
            operation,
            "Constant",
            $"{item.ConstantId}/{item.ValidFrom:yyyy-MM-dd}",
            originalValue: null,
            updatedValue:
                $"ConstantName={item.ConstantName}; " +
                $"Value={item.Value}; ValidTo={item.ValidTo}",
            success: true,
            message,
            cancellationToken);
}