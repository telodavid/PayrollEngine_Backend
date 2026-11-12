namespace PayrollEngine.AuthorizationAndAuditApp;

public sealed record AuditLogEntry(
    DateTimeOffset Timestamp,
    int? UserId,
    string? UserName,
    string Operation,
    string ObjectType,
    string? ObjectId,
    string? OriginalValue,
    string? UpdatedValue,
    bool Success,
    string? Message);
