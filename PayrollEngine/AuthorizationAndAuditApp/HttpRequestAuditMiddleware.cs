using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PayrollEngine.AuthorizationAndAuditApp;

public sealed class HttpRequestAuditMiddleware(
    RequestDelegate next,
    IAuditLogWriter writer,
    ILogger<HttpRequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        Exception? requestException = null;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            requestException = exception;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = requestException is null
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;

            var userIdValue = context.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            int? userId = int.TryParse(userIdValue, out var parsedUserId)
                ? parsedUserId
                : null;

            var userName = context.User
                .FindFirst(ClaimTypes.Name)?.Value;

            var entry = new AuditLogEntry(
                timestamp,
                userId,
                userName,
                $"HTTP_{context.Request.Method}",
                "HttpRequest",
                context.Request.Path.Value,
                null,
                null,
                statusCode < StatusCodes.Status400BadRequest,
                $"StatusCode={statusCode}; DurationMs={stopwatch.ElapsedMilliseconds}; " +
                $"TraceId={context.TraceIdentifier}");

            try
            {
                await writer.WriteAsync(entry, CancellationToken.None);
            }
            catch (Exception auditException)
            {
                logger.LogError(
                    auditException,
                    "A HTTP-kérés auditnaplózása sikertelen. " +
                    "Method: {Method}, Path: {Path}",
                    context.Request.Method,
                    context.Request.Path);
            }
        }
    }
}