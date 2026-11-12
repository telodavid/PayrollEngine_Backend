using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.PayrollRunApp.Models;

namespace PayrollEngine.PayrollRunApp;

public static class PayrollRunEndpoints
{
    public static WebApplication MapPayrollRunEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/payroll-runs/employee-preview",
                PreviewEmployeesAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_RUN"))
            .WithName("PreviewPayrollRunEmployees");

        app.MapPost(
                "/api/payroll-runs/execute",
                ExecuteAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_RUN"))
            .WithName("ExecutePayrollRun");

        app.MapGet(
                "/api/payroll-runs",
                GetRunHistoryAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_RUN"))
            .WithName("GetPayrollRunHistory");

        app.MapGet(
                "/api/payroll-runs/{runId:guid}",
                GetRunByIdAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "PAYROLL_RUN"))
            .WithName("GetPayrollRunById");

        return app;
    }

    private static async Task<IResult> PreviewEmployeesAsync(
        PayrollRunRequest request,
        PayrollRunEmployeeSelectionRepository employees,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);

        if (validationError is not null)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "PREVIEW",
                objectId: null,
                success: false,
                message: validationError,
                details: RequestDetails(request),
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var selectedEmployees = await employees.SelectAsync(
                request,
                cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "PREVIEW",
                objectId: null,
                success: true,
                message: "A számfejtésben részt vevő dolgozók előnézete elkészült.",
                details: $"{RequestDetails(request)}; " +
                         $"SelectedEmployeeCount={selectedEmployees.Count}",
                cancellationToken);

            return Results.Ok(new
            {
                request.SchemaCode,
                request.PeriodFrom,
                request.PeriodTo,
                request.IsSimulation,
                employeeCount = selectedEmployees.Count,
                employees = selectedEmployees,
                message = "A dolgozók előnézete elkészült."
            });
        }
        catch (SqlException exception)
            when (exception.Number is >= 50400 and <= 50406)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "PREVIEW",
                objectId: null,
                success: false,
                message: exception.Message,
                details: RequestDetails(request),
                cancellationToken);

            return Results.BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollRunEndpoints").LogError(
                exception,
                "Hiba történt a számfejtési dolgozók előnézésekor.");

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "PREVIEW",
                objectId: null,
                success: false,
                message: "A dolgozók előnézete sikertelen.",
                details: RequestDetails(request),
                cancellationToken);

            return Results.Problem(
                title: "A dolgozók előnézete sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ExecuteAsync(
        PayrollRunRequest request,
        PayrollRunExecutionService executionService,
        PayrollRunLogFileStorageService logStorage,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);

        if (validationError is not null)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: validationError,
                details: RequestDetails(request),
                cancellationToken);

            return Results.BadRequest(new { message = validationError });
        }

        var userId = GetUserId(httpContext);
        var userName = httpContext.User.Identity?.Name;

        if (!request.IsSimulation && userId is null)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: "Éles számfejtéshez bejelentkezett felhasználó szükséges.",
                details: RequestDetails(request),
                cancellationToken);

            return Results.Unauthorized();
        }

        try
        {
            var runLog = await executionService.ExecuteAsync(
                request,
                userId,
                userName,
                cancellationToken);

            await logStorage.SaveAsync(runLog, cancellationToken);

            var succeeded = runLog.Status is "Succeeded" or "NoPeriods";

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: runLog.RunId.ToString("D"),
                success: succeeded,
                message: succeeded
                    ? "A számfejtési futás befejeződött."
                    : "A számfejtési futás hibával vagy részleges eredménnyel zárult.",
                details: $"{RequestDetails(request)}; RunId={runLog.RunId}; " +
                         $"Status={runLog.Status}; " +
                         $"EmployeeCount={runLog.Employees.Count}",
                cancellationToken);

            return Results.Ok(runLog);
        }
        catch (OperationCanceledException)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: "A számfejtési futást megszakították.",
                details: RequestDetails(request),
                cancellationToken);

            throw;
        }
        catch (FileNotFoundException exception)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: exception.Message,
                details: RequestDetails(request),
                cancellationToken);

            return Results.NotFound(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: exception.Message,
                details: RequestDetails(request),
                cancellationToken);

            return Results.BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: exception.Message,
                details: RequestDetails(request),
                cancellationToken);

            return Results.Conflict(new { message = exception.Message });
        }
        catch (SqlException exception)
            when (exception.Number is >= 50400 and <= 50406)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: exception.Message,
                details: RequestDetails(request),
                cancellationToken);

            return Results.BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollRunEndpoints").LogError(
                exception,
                "Hiba történt a számfejtési futás során.");

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "EXECUTE",
                objectId: null,
                success: false,
                message: "A számfejtési futás sikertelen.",
                details: RequestDetails(request),
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési futás sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetRunHistoryAsync(
        PayrollRunLogFileStorageService logStorage,
        AuditLogService auditLog,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var runs = await logStorage.GetAllAsync(cancellationToken);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ_HISTORY",
                objectId: null,
                success: true,
                message: "A számfejtési futások listázása sikerült.",
                details: $"RunCount={runs.Count}",
                cancellationToken);

            return Results.Ok(runs);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollRunEndpoints").LogError(
                exception,
                "Hiba történt a számfejtési futások listázásakor.");

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ_HISTORY",
                objectId: null,
                success: false,
                message: "A számfejtési futások listázása sikertelen.",
                details: null,
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési futások lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetRunByIdAsync(
        Guid runId,
        PayrollRunLogFileStorageService logStorage,
        AuditLogService auditLog,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ_RUN",
                objectId: runId.ToString("D"),
                success: false,
                message: "Érvénytelen számfejtési futásazonosító.",
                details: null,
                cancellationToken);

            return Results.BadRequest(new
            {
                message = "Érvénytelen számfejtési futásazonosító."
            });
        }

        try
        {
            var runLog = await logStorage.GetByIdAsync(
                runId,
                cancellationToken);

            if (runLog is null)
            {
                await WriteAuditAsync(
                    auditLog,
                    httpContext,
                    operation: "READ_RUN",
                    objectId: runId.ToString("D"),
                    success: false,
                    message: "A számfejtési futás nem található.",
                    details: null,
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = "A számfejtési futás nem található."
                });
            }

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ_RUN",
                objectId: runId.ToString("D"),
                success: true,
                message: "A számfejtési futás részleteinek lekérdezése sikerült.",
                details: $"Status={runLog.Status}; SchemaCode={runLog.SchemaCode}",
                cancellationToken);

            return Results.Ok(runLog);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("PayrollRunEndpoints").LogError(
                exception,
                "Hiba történt a számfejtési futás lekérdezésekor. RunId: {RunId}",
                runId);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                operation: "READ_RUN",
                objectId: runId.ToString("D"),
                success: false,
                message: "A számfejtési futás lekérdezése sikertelen.",
                details: null,
                cancellationToken);

            return Results.Problem(
                title: "A számfejtési futás lekérdezése sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static string? ValidateRequest(PayrollRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SchemaCode))
        {
            return "A sémakód megadása kötelező.";
        }

        if (!TryParsePeriod(request.PeriodFrom, out var periodFrom))
        {
            return "A kezdő periódust yyyy-MM formátumban kell megadni.";
        }

        if (!TryParsePeriod(request.PeriodTo, out var periodTo))
        {
            return "A záró periódust yyyy-MM formátumban kell megadni.";
        }

        if (periodFrom > periodTo)
        {
            return "A kezdő periódus nem lehet későbbi a záró periódusnál.";
        }

        if (request.EmployeeIds is not null &&
            (request.EmployeeIdFrom.HasValue || request.EmployeeIdTo.HasValue))
        {
            return "Egyedi dolgozólista és ID-tartomány együtt nem adható meg.";
        }

        if (request.EmployeeIds is { Count: 0 })
        {
            return "A dolgozólista nem lehet üres.";
        }

        if (request.EmployeeIds?.Any(id => id <= 0) == true)
        {
            return "A dolgozóazonosítóknak pozitív számoknak kell lenniük.";
        }

        if (request.EmployeeIdFrom.HasValue != request.EmployeeIdTo.HasValue)
        {
            return "Az ID-tartomány kezdő és záró értékét is meg kell adni.";
        }

        if (request.EmployeeIdFrom is <= 0 || request.EmployeeIdTo is <= 0)
        {
            return "A dolgozó-ID-tartomány értékeinek pozitívnak kell lenniük.";
        }

        if (request.EmployeeIdFrom.HasValue &&
            request.EmployeeIdFrom.Value > request.EmployeeIdTo!.Value)
        {
            return "Az ID-tartomány kezdete nem lehet nagyobb a végénél.";
        }

        if (request.CompanyId is <= 0)
        {
            return "A cégazonosítónak pozitív számnak kell lennie.";
        }

        return null;
    }

    private static bool TryParsePeriod(string? value, out DateOnly period)
    {
        if (DateOnly.TryParseExact(
                value,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            period = new DateOnly(parsed.Year, parsed.Month, 1);
            return true;
        }

        period = default;
        return false;
    }

    private static int? GetUserId(HttpContext httpContext)
    {
        var value = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(value, out var userId) && userId > 0
            ? userId
            : null;
    }

    private static string RequestDetails(PayrollRunRequest request)
    {
        var employeeIds = request.EmployeeIds is null
            ? "*"
            : string.Join(',', request.EmployeeIds);

        return $"SchemaCode={request.SchemaCode}; " +
               $"PeriodFrom={request.PeriodFrom}; " +
               $"PeriodTo={request.PeriodTo}; " +
               $"EmployeeIds={employeeIds}; " +
               $"EmployeeIdFrom={request.EmployeeIdFrom}; " +
               $"EmployeeIdTo={request.EmployeeIdTo}; " +
               $"CompanyId={request.CompanyId}; " +
               $"IncludeInactive={request.IncludeInactive}; " +
               $"IsSimulation={request.IsSimulation}";
    }

    private static Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string operation,
        string? objectId,
        bool success,
        string message,
        string? details,
        CancellationToken cancellationToken)
    {
        return auditLog.WriteAsync(
            httpContext,
            operation: operation,
            objectType: "PayrollRun",
            objectId: objectId,
            originalValue: null,
            updatedValue: details,
            success: success,
            message: message,
            cancellationToken: cancellationToken);
    }
}
