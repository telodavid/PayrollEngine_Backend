using System.Globalization;
using System.Text.RegularExpressions;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportEmployeeDocumentFileExportService(
    ReportEmployeeDocumentBuilder documentBuilder,
    ReportEmployeeDocumentExportService documentExporter)
{
    public async Task<IReadOnlyList<ReportExportFile>> CreateFilesAsync(
        ReportDefinition report,
        ReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        var format = request.Format?.ToUpperInvariant();

        if (format is not ("XLSX" or "PDF"))
        {
            throw new ArgumentException(
                "A dolgozónkénti dokumentum XLSX vagy PDF " +
                "formátumban exportálható.");
        }

        var documents = await documentBuilder.BuildAsync(
            report,
            request,
            cancellationToken);

        if (documents.Count == 0)
        {
            throw new InvalidOperationException(
                "A megadott feltételekkel nem található " +
                "dolgozónként exportálható adat.");
        }

        var files = new List<ReportExportFile>();

        var usedNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = format == "XLSX"
                ? documentExporter.CreateXlsx(document)
                : documentExporter.CreatePdf(document);

            var extension = format.ToLowerInvariant();

            var contentType = format == "XLSX"
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "application/pdf";

            var fileName = CreateFileName(
                report,
                document,
                extension);

            if (!usedNames.Add(fileName))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő dokumentumfájlnév: {fileName}");
            }

            files.Add(new ReportExportFile(
                fileName,
                contentType,
                content));
        }

        return files;
    }

    private static string CreateFileName(
        ReportDefinition report,
        ReportEmployeeDocumentData document,
        string extension)
    {
        var pattern = string.IsNullOrWhiteSpace(report.FileNamePattern)
            ? $"{report.ReportCode}_{{employeeId}}"
            : report.FileNamePattern;

        var containsEmployeeId = false;

        var expanded = Regex.Replace(
            pattern,
            @"\{([^{}]+)\}",
            match =>
            {
                var token = match.Groups[1].Value.Trim();

                if (string.Equals(
                        token.Split('.').Last(),
                        "EmployeeId",
                        StringComparison.OrdinalIgnoreCase))
                {
                    containsEmployeeId = true;

                    return document.EmployeeId.ToString(
                        CultureInfo.InvariantCulture);
                }

                return ResolveToken(document, token);
            });

        var name = SanitizeFileName(expanded);

        if (string.IsNullOrWhiteSpace(name))
        {
            name = SanitizeFileName(report.ReportCode);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "riport";
        }

        if (name.Length > 160)
        {
            name = name[..160];

            if (char.IsHighSurrogate(name[^1]))
            {
                name = name[..^1];
            }

            containsEmployeeId = false;
        }

        if (!containsEmployeeId)
        {
            name += $"_dolgozo_{document.EmployeeId}";
        }

        return $"{name}.{extension}";
    }

    private static string ResolveToken(
        ReportEmployeeDocumentData document,
        string token)
    {
        if (document.FileNameValues.TryGetValue(
                token,
                out var directValue))
        {
            return FormatTokenValue(directValue);
        }

        var matches = document.FileNameValues
            .Where(item => string.Equals(
                item.Key.Split('.').Last(),
                token,
                StringComparison.OrdinalIgnoreCase))
            .Select(item => FormatTokenValue(item.Value))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();

        if (matches.Length == 1)
        {
            return matches[0];
        }

        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"A fájlnévminta hivatkozása nem egyértelmű: {token}");
        }

        throw new InvalidOperationException(
            $"A fájlnévminta ismeretlen adatra hivatkozik: {token}");
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = new[]
        {
            '<', '>', ':', '"', '/', '\\', '|', '?', '*'
        };

        return new string(value
                .Select(character =>
                    char.IsControl(character) ||
                    invalidCharacters.Contains(character)
                        ? '_'
                        : character)
                .ToArray())
            .Trim()
            .Trim('.')
            .Trim();
    }

    private static string FormatTokenValue(object? value)
    {
        return value switch
        {
            null => "",
            DateOnly date => date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? ""
        };
    }
}