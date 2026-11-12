using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PayrollEngine.AuthorizationAndAuditApp;

public sealed class AuditLogService(IAuditLogWriter writer)
{
    public Task WriteAsync(
        HttpContext httpContext,
        string operation,
        string objectType,
        string? objectId,
        string? originalValue,
        string? updatedValue,
        bool success,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var userIdValue = httpContext.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        int? userId = int.TryParse(userIdValue, out var parsedUserId)
            ? parsedUserId
            : null;

        var userName = httpContext.User
            .FindFirst(ClaimTypes.Name)?.Value;

        var entry = new AuditLogEntry(
            DateTimeOffset.UtcNow,
            userId,
            userName,
            operation,
            objectType,
            objectId,
            originalValue,
            updatedValue,
            success,
            message);

        return writer.WriteAsync(entry, cancellationToken);
    }
}