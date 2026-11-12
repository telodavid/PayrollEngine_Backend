using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using PayrollEngine.PayrollRunApp.Models;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollRunLogFileStorageService
{
    private readonly string _runsDirectory;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public PayrollRunLogFileStorageService(
        IWebHostEnvironment environment)
    {
        _runsDirectory = Path.Combine(
            environment.ContentRootPath,
            "PayrollRunApp",
            "Runs");
    }

    public async Task SaveAsync(
        PayrollRunLog runLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runLog);

        Directory.CreateDirectory(_runsDirectory);

        var filePath = GetFilePath(runLog.RunId);
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(runLog, JsonOptions);

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                cancellationToken);

            File.Move(
                temporaryPath,
                filePath,
                overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<PayrollRunLog?> GetByIdAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException(
                "A futásazonosító nem lehet üres.",
                nameof(runId));
        }

        var filePath = GetFilePath(runId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(
            filePath,
            cancellationToken);

        return JsonSerializer.Deserialize<PayrollRunLog>(
            json,
            JsonOptions);
    }

    public async Task<IReadOnlyList<PayrollRunLog>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_runsDirectory);

        var logs = new List<PayrollRunLog>();

        foreach (var filePath in Directory.EnumerateFiles(
                     _runsDirectory,
                     "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var json = await File.ReadAllTextAsync(
                filePath,
                cancellationToken);

            var runLog = JsonSerializer.Deserialize<PayrollRunLog>(
                json,
                JsonOptions);

            if (runLog is not null)
            {
                logs.Add(runLog);
            }
        }

        return logs
            .OrderByDescending(runLog => runLog.StartedAtUtc)
            .ToArray();
    }

    private string GetFilePath(Guid runId)
        => Path.Combine(_runsDirectory, $"{runId:N}.json");
}