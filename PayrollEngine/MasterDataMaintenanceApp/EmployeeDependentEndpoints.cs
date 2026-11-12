using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.MasterDataMaintenanceApp;

public static class EmployeeDependentEndpoints
{
    public static WebApplication MapEmployeeDependentEndpoints(
        this WebApplication app)
    {
        var endpoints = app
            .MapGroup("/api/employees/{employeeId:int}/dependents")
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "MASTERDATA_MAINTENANCE"));

        endpoints.MapGet("", GetByEmployeeIdAsync);
        endpoints.MapGet("/relationship-types", GetRelationshipTypesAsync);
        endpoints.MapPost("", CreateAsync);
        endpoints.MapPut("/{employeeDependentId:int}", UpdateAsync);
        endpoints.MapDelete("/{employeeDependentId:int}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetByEmployeeIdAsync(
        int employeeId,
        EmployeeDependentRepository repository,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen munkavállaló-azonosító."
            });
        }

        try
        {
            return Results.Ok(
                await repository.GetByEmployeeIdAsync(
                    employeeId,
                    cancellationToken));
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static async Task<IResult> GetRelationshipTypesAsync(
        int employeeId,
        EmployeeDependentRepository repository,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen munkavállaló-azonosító."
            });
        }

        try
        {
            return Results.Ok(
                await repository.GetRelationshipTypesAsync(cancellationToken));
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static async Task<IResult> CreateAsync(
        int employeeId,
        SaveEmployeeDependentRequest request,
        EmployeeDependentRepository repository,
        AuditLogService auditLog,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = Validate(employeeId, request);

        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        try
        {
            var result = await repository.CreateAsync(
                employeeId,
                Normalize(request),
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                "CREATE_EMPLOYEE_DEPENDENT",
                employeeId,
                result.EmployeeDependentId,
                request,
                "Az eltartott rögzítése sikerült.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int employeeId,
        int employeeDependentId,
        SaveEmployeeDependentRequest request,
        EmployeeDependentRepository repository,
        AuditLogService auditLog,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = Validate(employeeId, request);

        if (validation is not null || employeeDependentId <= 0)
        {
            return Results.BadRequest(new
            {
                message = validation ?? "Érvénytelen eltartott-azonosító."
            });
        }

        try
        {
            var result = await repository.UpdateAsync(
                employeeId,
                employeeDependentId,
                Normalize(request),
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                "UPDATE_EMPLOYEE_DEPENDENT",
                employeeId,
                employeeDependentId,
                request,
                "Az eltartott adatai módosultak.",
                cancellationToken);

            return Results.Ok(result);
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static async Task<IResult> DeleteAsync(
        int employeeId,
        int employeeDependentId,
        EmployeeDependentRepository repository,
        AuditLogService auditLog,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (employeeId <= 0 || employeeDependentId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "Érvénytelen dolgozó- vagy eltartott-azonosító."
            });
        }

        try
        {
            await repository.DeleteAsync(
                employeeId,
                employeeDependentId,
                cancellationToken);

            await auditLog.WriteAsync(
                httpContext,
                operation: "DELETE_EMPLOYEE_DEPENDENT",
                objectType: "EmployeeDependent",
                objectId: employeeDependentId.ToString(),
                originalValue: $"EmployeeId={employeeId}",
                updatedValue: null,
                success: true,
                message: "Az eltartott hozzárendelését töröltük.",
                cancellationToken: cancellationToken);

            return Results.Ok(new
            {
                message = "Az eltartott hozzárendelését töröltük."
            });
        }
        catch (SqlException exception)
        {
            return DatabaseError(exception);
        }
    }

    private static string? Validate(
        int employeeId,
        SaveEmployeeDependentRequest request)
    {
        if (employeeId <= 0)
        {
            return "Érvénytelen munkavállaló-azonosító.";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName)
            || request.FirstName.Trim().Length > 100)
        {
            return "A keresztnév megadása kötelező, legfeljebb 100 karakter lehet.";
        }

        if (string.IsNullOrWhiteSpace(request.LastName)
            || request.LastName.Trim().Length > 100)
        {
            return "A vezetéknév megadása kötelező, legfeljebb 100 karakter lehet.";
        }

        if (request.TaxId is { } taxId && taxId.Trim().Length > 30)
        {
            return "Az adóazonosító legfeljebb 30 karakter lehet.";
        }

        if (request.RelationshipTypeId <= 0)
        {
            return "Válassz rokonsági típust.";
        }

        if (request.ValidFrom.HasValue
            && request.ValidTo.HasValue
            && request.ValidTo.Value < request.ValidFrom.Value)
        {
            return "Az érvényesség vége nem lehet korábbi a kezdeténél.";
        }

        return null;
    }

    private static SaveEmployeeDependentRequest Normalize(
        SaveEmployeeDependentRequest request)
        => request with
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            TaxId = string.IsNullOrWhiteSpace(request.TaxId)
                ? null
                : request.TaxId.Trim()
        };

    private static IResult DatabaseError(SqlException exception)
        => exception.Number switch
        {
            50701 => Results.NotFound(new
            {
                message = "A munkavállaló nem található."
            }),
            50702 => Results.NotFound(new
            {
                message = "A rokonsági típus nem található."
            }),
            50703 => Results.NotFound(new
            {
                message = "Az eltartott-hozzárendelés nem található."
            }),
            50704 => Results.BadRequest(new
            {
                message = exception.Message
            }),
            _ => Results.Problem(
                title: "Az eltartott adatainak kezelése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError)
        };

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string operation,
        int employeeId,
        int employeeDependentId,
        SaveEmployeeDependentRequest request,
        string message,
        CancellationToken cancellationToken)
        => auditLog.WriteAsync(
            httpContext,
            operation,
            "EmployeeDependent",
            employeeDependentId.ToString(),
            originalValue: null,
            updatedValue:
                $"EmployeeId={employeeId}; " +
                $"RelationshipTypeId={request.RelationshipTypeId}; " +
                $"ValidFrom={request.ValidFrom}; ValidTo={request.ValidTo}",
            success: true,
            message,
            cancellationToken);
}