namespace PayrollEngine.AuthorizationAndAuditApp;

public interface IAuditLogWriter
{
    Task WriteAsync(
        AuditLogEntry entry,
        CancellationToken cancellationToken = default);
}