using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public static class ReportExportEndpoints
{
    public static WebApplication MapReportExportEndpoints(
        this WebApplication app)
    {
        app.MapPost(
                "/api/reports/{reportCode}/export",
                ExportAsync)
            .RequireAuthorization(policy =>
                policy.RequireClaim("app", "REPORT_BUILDER"))
            .WithName("ExportReport");

        return app;
    }

    private static async Task<IResult> ExportAsync(
        string reportCode,
        ReportExportRequest request,
        ReportDefinitionFileStorageService definitions,
        ReportDataRepository dataRepository,
        ReportFileExportService tableExporter,
        ReportEmployeeDocumentFileExportService documentExporter,
        HttpContext httpContext,
        AuditLogService auditLog,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await definitions.GetByCodeAsync(
                reportCode,
                cancellationToken);

            if (report is null)
            {
                await WriteAuditAsync(
                    auditLog,
                    httpContext,
                    reportCode,
                    request,
                    false,
                    "A riportdefiníció nem található.",
                    null,
                    cancellationToken);

                return Results.NotFound(new
                {
                    message = $"A riport nem található: {reportCode}"
                });
            }

            if (!report.IsActive)
            {
                await WriteAuditAsync(
                    auditLog,
                    httpContext,
                    reportCode,
                    request,
                    false,
                    "Inaktív riport nem exportálható.",
                    null,
                    cancellationToken);

                return Results.Conflict(new
                {
                    message = "Inaktív riport nem exportálható."
                });
            }

            var layoutMode = string.IsNullOrWhiteSpace(report.LayoutMode)
                ? "TABLE"
                : report.LayoutMode.Trim().ToUpperInvariant();

            IReadOnlyList<ReportExportFile> files;
            int? sourceRowCount = null;

            switch (layoutMode)
            {
                case "TABLE":
                    {
                        var sourceRows = await dataRepository.LoadAsync(
                            report,
                            request,
                            cancellationToken);

                        sourceRowCount = sourceRows.Count;

                        files = tableExporter.CreateFiles(
                            report,
                            sourceRows,
                            request.Format,
                            request.PeriodFrom,
                            request.PeriodTo);

                        break;
                    }

                case "EMPLOYEE_DOCUMENT":
                    {
                        files = await documentExporter.CreateFilesAsync(
                            report,
                            request,
                            cancellationToken);

                        break;
                    }

                default:
                    throw new InvalidOperationException(
                        $"Ismeretlen riportmegjelenítési mód: {report.LayoutMode}");
            }

            if (files.Count == 0)
            {
                throw new InvalidOperationException(
                    "A megadott feltételekkel nem készült exportálható fájl.");
            }

            IResult result;

            if (files.Count == 1)
            {
                var file = files[0];

                result = Results.File(
                    file.Content,
                    file.ContentType,
                    file.FileName);
            }
            else
            {
                var archive = await CreateZipAsync(
                    files,
                    cancellationToken);

                result = Results.File(
                    archive,
                    "application/zip",
                    $"{report.ReportCode}_export.zip");
            }

            var details =
                $"LayoutMode={layoutMode}; FileCount={files.Count}";

            if (sourceRowCount.HasValue)
            {
                details += $"; RecordCount={sourceRowCount.Value}";
            }

            await WriteAuditAsync(
                auditLog,
                httpContext,
                reportCode,
                request,
                true,
                "A riport exportálása sikerült.",
                details,
                cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  InvalidOperationException or
                  KeyNotFoundException)
        {
            await WriteAuditAsync(
                auditLog,
                httpContext,
                reportCode,
                request,
                false,
                exception.Message,
                null,
                cancellationToken);

            return Results.BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            var logger = loggerFactory.CreateLogger(
                nameof(ReportExportEndpoints));

            logger.LogError(
                exception,
                "Hiba történt a riport exportálásakor. " +
                "ReportCode: {ReportCode}",
                reportCode);

            await WriteAuditAsync(
                auditLog,
                httpContext,
                reportCode,
                request,
                false,
                "A riport exportálása sikertelen.",
                null,
                cancellationToken);

            return Results.Problem(
                title: "A riport exportálása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteAuditAsync(
        AuditLogService auditLog,
        HttpContext httpContext,
        string reportCode,
        ReportExportRequest request,
        bool success,
        string message,
        string? details,
        CancellationToken cancellationToken)
    {
        var updatedValue =
            $"Format={request.Format}; " +
            $"PeriodFrom={request.PeriodFrom}; " +
            $"PeriodTo={request.PeriodTo}; " +
            $"AsOfDate={request.AsOfDate:yyyy-MM-dd}; " +
            $"DateFrom={request.DateFrom:yyyy-MM-dd}; " +
            $"DateTo={request.DateTo:yyyy-MM-dd}; " +
            $"CompanyId={request.CompanyId}; " +
            $"EmployeeCount={request.EmployeeIds?.Count}";

        if (!string.IsNullOrWhiteSpace(details))
        {
            updatedValue += $"; {details}";
        }

        await auditLog.WriteAsync(
            httpContext,
            operation: "EXPORT",
            objectType: "Report",
            objectId: reportCode,
            originalValue: null,
            updatedValue: updatedValue,
            success: success,
            message: message,
            cancellationToken);
    }

    private static async Task<byte[]> CreateZipAsync(
        IReadOnlyList<ReportExportFile> files,
        CancellationToken cancellationToken)
    {
        await using var memoryStream = new MemoryStream();

        var usedNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        using (var archive = new ZipArchive(
                   memoryStream,
                   ZipArchiveMode.Create,
                   leaveOpen: true))
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!usedNames.Add(file.FileName))
                {
                    throw new InvalidOperationException(
                        $"Ismétlődő exportfájlnév: {file.FileName}");
                }

                var entry = archive.CreateEntry(
                    file.FileName,
                    CompressionLevel.Fastest);

                await using var entryStream = entry.Open();

                await entryStream.WriteAsync(
                    file.Content,
                    cancellationToken);
            }
        }

        return memoryStream.ToArray();
    }
}