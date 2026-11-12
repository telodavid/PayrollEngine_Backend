using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportFileExportService(
    ReportOutputRowBuilder rowBuilder,
    ReportOfficeExportService officeExporter)
{
    public IReadOnlyList<ReportExportFile> CreateFiles(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> sourceRows,
        string format,
        string? periodFrom = null,
        string? periodTo = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(sourceRows);

        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException(
                "Az exportformátum megadása kötelező.",
                nameof(format));
        }

        var normalizedFormat = format.Trim().ToUpperInvariant();

        if (normalizedFormat is not ("XML" or "TXT" or "XLSX" or "PDF"))
        {
            throw new ArgumentException(
                "A támogatott exportformátumok: XML, TXT, XLSX és PDF.",
                nameof(format));
        }

        var fileGroups = SplitRows(report, sourceRows);

        return fileGroups
            .Select(group =>
            {
                var outputRows = rowBuilder.Build(report, group.Rows);

                var (content, contentType, extension) =
                    CreateContent(report, outputRows, normalizedFormat);

                var fileName = CreateFileName(
                    report,
                    group.Rows,
                    extension,
                    periodFrom,
                    periodTo);

                return new ReportExportFile(
                    fileName,
                    contentType,
                    content);
            })
            .ToArray();
    }

    private (
        byte[] Content,
        string ContentType,
        string Extension) CreateContent(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string format)
    {
        return format switch
        {
            "XML" => (
                CreateXml(report, rows),
                "application/xml",
                "xml"),

            "TXT" => (
                CreateText(report, rows),
                "text/plain; charset=utf-8",
                "txt"),

            "XLSX" => (
                officeExporter.CreateXlsx(report, rows),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "xlsx"),

            "PDF" => (
                officeExporter.CreatePdf(report, rows),
                "application/pdf",
                "pdf"),

            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static IReadOnlyList<SourceRowGroup> SplitRows(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var splitReferences = report.Grouping.SplitFilesBy;

        if (splitReferences.Count == 0)
        {
            return [new SourceRowGroup("all", rows)];
        }

        if (rows.Count == 0)
        {
            return [new SourceRowGroup("empty", [])];
        }

        return rows
            .GroupBy(
                row => string.Join(
                    "|",
                    splitReferences.Select(reference =>
                        FormatValue(FindValue(row, reference)) ?? "")),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new SourceRowGroup(
                group.Key,
                group.ToArray()))
            .ToArray();
    }

    private static byte[] CreateXml(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var rootName = XmlConvert.VerifyName(
            string.IsNullOrWhiteSpace(report.XmlRootElementName)
                ? "Report"
                : report.XmlRootElementName);

        var recordName = XmlConvert.VerifyName(
            string.IsNullOrWhiteSpace(report.XmlRecordElementName)
                ? "Record"
                : report.XmlRecordElementName);

        var root = new XElement(rootName);

        foreach (var row in rows)
        {
            var record = new XElement(recordName);

            foreach (var field in report.Fields)
            {
                var outputName = XmlConvert.VerifyName(field.OutputName);
                row.TryGetValue(field.OutputName, out var value);

                record.Add(new XElement(
                    outputName,
                    FormatValue(value) is { } text
                        ? new XText(text)
                        : null));
            }

            root.Add(record);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            root);

        return Encoding.UTF8.GetBytes(document.ToString());
    }

    private static byte[] CreateText(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var builder = new StringBuilder();

        builder.AppendLine(string.Join(
            '\t',
            report.Fields.Select(field => EscapeText(field.OutputName))));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(
                '\t',
                report.Fields.Select(field =>
                {
                    row.TryGetValue(field.OutputName, out var value);
                    return EscapeText(FormatValue(value) ?? "");
                })));
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            .GetBytes(builder.ToString());
    }

    private static string CreateFileName(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string extension,
        string? periodFrom,
        string? periodTo)
    {
        var sourceRow = rows.FirstOrDefault();
        var pattern = string.IsNullOrWhiteSpace(report.FileNamePattern)
            ? $"{report.ReportCode}_{{period}}"
            : report.FileNamePattern;

        pattern = Regex.Replace(
            pattern,
            @"\{([^{}]+)\}",
            match =>
            {
                var token = match.Groups[1].Value;

                if (string.Equals(
                        token,
                        "period",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return SanitizeFilePart(periodFrom ?? "period");
                }

                if (string.Equals(
                        token,
                        "periodFrom",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return SanitizeFilePart(periodFrom ?? "");
                }

                if (string.Equals(
                        token,
                        "periodTo",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return SanitizeFilePart(periodTo ?? "");
                }

                if (sourceRow is not null)
                {
                    var value = FindTokenValue(sourceRow, token);

                    if (value is not null)
                    {
                        return SanitizeFilePart(value);
                    }
                }

                return "unknown";
            });

        var safeName = SanitizeFilePart(pattern);

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = SanitizeFilePart(report.ReportCode);
        }

        return $"{safeName}.{extension}";
    }

    private static string? FindTokenValue(
        IReadOnlyDictionary<string, object?> row,
        string token)
    {
        var match = row.FirstOrDefault(item =>
            string.Equals(
                item.Key.Split('.').Last(),
                token,
                StringComparison.OrdinalIgnoreCase));

        return match.Key is null
            ? null
            : FormatValue(match.Value);
    }

    private static object? FindValue(
        IReadOnlyDictionary<string, object?> row,
        string reference)
    {
        if (row.TryGetValue(reference, out var value))
        {
            return value;
        }

        var match = row.FirstOrDefault(item =>
            string.Equals(
                item.Key,
                reference,
                StringComparison.OrdinalIgnoreCase));

        if (match.Key is null)
        {
            throw new KeyNotFoundException(
                $"A fájlszétválasztó hivatkozás nem található: {reference}");
        }

        return match.Value;
    }

    private static string EscapeText(string value)
    {
        if (!value.Contains('\t') &&
            !value.Contains('\r') &&
            !value.Contains('\n') &&
            !value.Contains('"'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string SanitizeFilePart(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();

        var sanitized = new string(value
            .Select(character =>
                invalidCharacters.Contains(character) ||
                character is '/' or '\\'
                    ? '_'
                    : character)
            .ToArray());

        return sanitized.Trim().Trim('.');
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            DateOnly date => date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString(
                "O",
                CultureInfo.InvariantCulture),
            TimeSpan time => time.ToString(
                "c",
                CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private sealed record SourceRowGroup(
        string Key,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
}

public sealed record ReportExportFile(
    string FileName,
    string ContentType,
    byte[] Content);