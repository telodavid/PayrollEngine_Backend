using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using System.Text.Encodings.Web;

namespace PayrollEngine.AuthorizationAndAuditApp;

public sealed class FileAuditLogWriter(
    IHostEnvironment environment) : IAuditLogWriter
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static readonly JsonSerializerOptions SerializerOptions =
    new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task WriteAsync(
        AuditLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var directory = Path.Combine(
            environment.ContentRootPath,
            "AuditLogs");

        Directory.CreateDirectory(directory);

        var date = entry.Timestamp.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        var filePath = Path.Combine(directory, $"audit-{date}.txt");
        var jsonLine = JsonSerializer.Serialize(entry, SerializerOptions);

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            await File.AppendAllTextAsync(
                filePath,
                jsonLine + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}