using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportDefinitionFileStorageService
{
    private readonly string _reportsDirectory;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public ReportDefinitionFileStorageService(
        IWebHostEnvironment environment)
    {
        _reportsDirectory = Path.Combine(
            environment.ContentRootPath,
            "ReportBuilderApp",
            "Reports");
    }

    public async Task<IReadOnlyList<ReportDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_reportsDirectory);

        var reports = new List<ReportDefinition>();

        foreach (var filePath in Directory.EnumerateFiles(
                     _reportsDirectory,
                     "*.json"))
        {
            await using var stream = File.OpenRead(filePath);

            var report =
                await JsonSerializer.DeserializeAsync<ReportDefinition>(
                    stream,
                    JsonOptions,
                    cancellationToken);

            if (report is not null)
            {
                reports.Add(report);
            }
        }

        return reports
            .OrderBy(report => report.Name)
            .ToArray();
    }

    public async Task<ReportDefinition?> GetByCodeAsync(
        string reportCode,
        CancellationToken cancellationToken = default)
    {
        ValidateReportCode(reportCode);

        var filePath = FindFilePath(reportCode);

        if (filePath is null)
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);

        return await JsonSerializer.DeserializeAsync<ReportDefinition>(
            stream,
            JsonOptions,
            cancellationToken);
    }

    public async Task CreateAsync(
        ReportDefinition report,
        CancellationToken cancellationToken = default)
    {
        ValidateReport(report);

        var existingReports = await GetAllAsync(cancellationToken);

        if (existingReports.Any(existing =>
                string.Equals(
                    existing.ReportCode,
                    report.ReportCode,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Már létezik ilyen riport: {report.ReportCode}");
        }

        await WriteAsync(
            GetFilePath(report.ReportCode),
            report,
            cancellationToken);
    }

    public async Task UpdateAsync(
        string reportCode,
        ReportDefinition report,
        CancellationToken cancellationToken = default)
    {
        ValidateReportCode(reportCode);
        ValidateReport(report);

        if (!string.Equals(
                reportCode,
                report.ReportCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A riport kódja szerkesztéskor nem módosítható.",
                nameof(report));
        }

        var filePath = FindFilePath(reportCode);

        if (filePath is null)
        {
            throw new FileNotFoundException(
                $"A riport nem található: {reportCode}");
        }

        report.ReportCode = reportCode;

        await WriteAsync(filePath, report, cancellationToken);
    }

    public async Task<ReportDefinition?> DeleteAsync(
        string reportCode,
        CancellationToken cancellationToken = default)
    {
        ValidateReportCode(reportCode);

        var filePath = FindFilePath(reportCode);

        if (filePath is null)
        {
            return null;
        }

        ReportDefinition? report;

        await using (var stream = File.OpenRead(filePath))
        {
            report =
                await JsonSerializer.DeserializeAsync<ReportDefinition>(
                    stream,
                    JsonOptions,
                    cancellationToken);
        }

        File.Delete(filePath);

        return report;
    }

    private async Task WriteAsync(
        string filePath,
        ReportDefinition report,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_reportsDirectory);

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(report, JsonOptions);

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                cancellationToken);

            File.Move(
                temporaryPath,
                filePath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string? FindFilePath(string reportCode)
    {
        Directory.CreateDirectory(_reportsDirectory);

        return Directory
            .EnumerateFiles(_reportsDirectory, "*.json")
            .FirstOrDefault(path =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    reportCode,
                    StringComparison.OrdinalIgnoreCase));
    }

    private string GetFilePath(string reportCode)
        => Path.Combine(_reportsDirectory, $"{reportCode}.json");

    private static void ValidateReport(ReportDefinition report)
    {
        ArgumentNullException.ThrowIfNull(report);

        ValidateReportCode(report.ReportCode);

        if (string.IsNullOrWhiteSpace(report.Name) ||
            report.Name.Length > 200)
        {
            throw new ArgumentException(
                "A riport neve nem lehet üres, és legfeljebb 200 karakter lehet.",
                nameof(report));
        }

        if (string.IsNullOrWhiteSpace(report.DataSource) ||
            report.DataSource.Length > 100)
        {
            throw new ArgumentException(
                "Az adatforrás nem lehet üres, és legfeljebb 100 karakter lehet.",
                nameof(report));
        }
    }

    private static void ValidateReportCode(string? reportCode)
    {
        if (string.IsNullOrWhiteSpace(reportCode) ||
            reportCode.Length > 100 ||
            reportCode.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '_' or '-')))
        {
            throw new ArgumentException(
                "A riportkód csak betűt, számot, aláhúzást és kötőjelet tartalmazhat, és legfeljebb 100 karakter lehet.",
                nameof(reportCode));
        }
    }
}