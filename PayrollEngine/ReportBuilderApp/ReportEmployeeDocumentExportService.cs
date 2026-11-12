using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using PayrollEngine.ReportBuilderApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportEmployeeDocumentExportService
{
    private static readonly CultureInfo DisplayCulture =
        CultureInfo.GetCultureInfo("hu-HU");

    public byte[] CreateXlsx(
        ReportEmployeeDocumentData document)
    {
        ValidateDocument(document);

        using var workbook = new XLWorkbook();

        var worksheetNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var section in document.Sections)
        {
            var worksheetName = CreateUniqueWorksheetName(
                section.WorksheetName,
                worksheetNames);

            var worksheet = workbook.Worksheets.Add(
                worksheetName);

            worksheet.Style.Font.FontName = "Calibri";
            worksheet.Style.Font.FontSize = 11;

            var titleWidth = Math.Max(
                section.Fields.Count,
                2);

            worksheet.Range(1, 1, 1, titleWidth).Merge();

            var titleCell = worksheet.Cell(1, 1);

            titleCell.Value = document.Title;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 16;

            worksheet.Row(1).Height = 26;

            var currentRow = 3;
            var headerRows = new List<int>();

            foreach (var item in document.Header)
            {
                var labelCell = worksheet.Cell(
                    currentRow,
                    1);

                labelCell.Value = item.Label;
                labelCell.Style.Font.Bold = true;
                labelCell.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Top;

                var valueCell = worksheet.Cell(
                    currentRow,
                    2);

                SetCellValue(valueCell, item.Value);

                if (!string.IsNullOrWhiteSpace(item.Format))
                {
                    valueCell.Style.NumberFormat.Format =
                        item.Format;
                }

                valueCell.Style.Alignment.WrapText = true;
                valueCell.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Top;

                headerRows.Add(currentRow);
                currentRow++;
            }

            if (document.Header.Count > 0)
            {
                currentRow++;
            }

            worksheet.Range(
                    currentRow,
                    1,
                    currentRow,
                    titleWidth)
                .Merge();

            var sectionTitle = worksheet.Cell(
                currentRow,
                1);

            sectionTitle.Value = section.Title;
            sectionTitle.Style.Font.Bold = true;
            sectionTitle.Style.Font.FontSize = 12;

            currentRow += 2;

            var tableHeaderRow = currentRow;

            if ((long)tableHeaderRow + section.OutputRows.Count >
                1_048_576)
            {
                throw new InvalidOperationException(
                    $"Az adatrész több sort tartalmaz, " +
                    $"mint amennyi egy Excel-munkalapon elfér: " +
                    section.Title);
            }

            for (var columnIndex = 0;
                 columnIndex < section.Fields.Count;
                 columnIndex++)
            {
                var item = section.Fields[columnIndex];

                worksheet.Cell(
                        tableHeaderRow,
                        columnIndex + 1)
                    .Value = FieldLabel(item);
            }

            var tableHeader = worksheet.Range(
                tableHeaderRow,
                1,
                tableHeaderRow,
                section.Fields.Count);

            tableHeader.Style.Font.Bold = true;
            tableHeader.Style.Fill.BackgroundColor =
                XLColor.LightGray;

            tableHeader.Style.Alignment.WrapText = true;

            for (var rowIndex = 0;
                 rowIndex < section.OutputRows.Count;
                 rowIndex++)
            {
                var sourceRow = section.OutputRows[rowIndex];

                for (var columnIndex = 0;
                     columnIndex < section.Fields.Count;
                     columnIndex++)
                {
                    var item = section.Fields[columnIndex];

                    sourceRow.TryGetValue(
                        item.OutputName,
                        out var value);

                    var cell = worksheet.Cell(
                        tableHeaderRow + rowIndex + 1,
                        columnIndex + 1);

                    SetCellValue(cell, value);

                    if (!string.IsNullOrWhiteSpace(item.Format))
                    {
                        cell.Style.NumberFormat.Format =
                            item.Format;
                    }
                }
            }

            var lastTableRow =
                tableHeaderRow + section.OutputRows.Count;

            if (section.OutputRows.Count > 0)
            {
                worksheet.Range(
                        tableHeaderRow,
                        1,
                        lastTableRow,
                        section.Fields.Count)
                    .SetAutoFilter();
            }

            worksheet.SheetView.FreezeRows(tableHeaderRow);

            for (var columnIndex = 1;
                 columnIndex <= titleWidth;
                 columnIndex++)
            {
                var column = worksheet.Column(columnIndex);

                column.AdjustToContents(
                    tableHeaderRow,
                    Math.Max(tableHeaderRow, lastTableRow));

                column.Width = Math.Clamp(
                    column.Width,
                    14,
                    45);
            }

            if (document.Header.Count > 0)
            {
                worksheet.Column(1).Width = Math.Max(
                    worksheet.Column(1).Width,
                    24);

                worksheet.Column(2).Width = Math.Max(
                    worksheet.Column(2).Width,
                    40);

                foreach (var rowNumber in headerRows)
                {
                    worksheet.Row(rowNumber).AdjustToContents();
                }
            }

            worksheet.Row(tableHeaderRow).AdjustToContents();

            if (section.OutputRows.Count == 0)
            {
                worksheet.Cell(tableHeaderRow + 1, 1)
                    .Value = "Nincs megjeleníthető adat.";
            }
        }

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    public byte[] CreatePdf(
        ReportEmployeeDocumentData document)
    {
        ValidateDocument(document);

        var landscape = document.Sections.Any(
            item => item.Fields.Count > 6);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(
                    landscape
                        ? PageSizes.A4.Landscape()
                        : PageSizes.A4);

                page.Margin(28);

                page.DefaultTextStyle(style =>
                    style.FontSize(9));

                page.Header()
                    .PaddingBottom(14)
                    .Column(column =>
                    {
                        column.Spacing(8);

                        column.Item()
                            .Text(document.Title)
                            .SemiBold()
                            .FontSize(16);

                        if (document.Header.Count > 0)
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                foreach (var item in document.Header)
                                {
                                    table.Cell()
                                        .PaddingVertical(2)
                                        .PaddingRight(10)
                                        .Text(item.Label)
                                        .SemiBold();

                                    table.Cell()
                                        .PaddingVertical(2)
                                        .Text(FormatValue(
                                            item.Value,
                                            item.Format));
                                }
                            });
                        }
                    });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    for (var index = 0;
                         index < document.Sections.Count;
                         index++)
                    {
                        var section = document.Sections[index];

                        if (index > 0 && section.StartNewPage)
                        {
                            column.Item().PageBreak();
                        }

                        column.Item()
                            .Text(section.Title)
                            .SemiBold()
                            .FontSize(12);

                        if (section.OutputRows.Count == 0)
                        {
                            column.Item()
                                .Text("Nincs megjeleníthető adat.")
                                .FontColor(Colors.Grey.Darken1);

                            continue;
                        }

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var item in section.Fields)
                                {
                                    columns.RelativeColumn();
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var item in section.Fields)
                                {
                                    header.Cell()
                                        .Element(HeaderCell)
                                        .Text(FieldLabel(item))
                                        .SemiBold();
                                }
                            });

                            foreach (var row in section.OutputRows)
                            {
                                foreach (var item in section.Fields)
                                {
                                    row.TryGetValue(
                                        item.OutputName,
                                        out var value);

                                    table.Cell()
                                        .Element(BodyCell)
                                        .Text(FormatValue(
                                            value,
                                            item.Format));
                                }
                            }
                        });
                    }
                });

                page.Footer()
                    .PaddingTop(10)
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Oldal ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
            });
        });

        return pdf.GeneratePdf();
    }

    private static void ValidateDocument(
        ReportEmployeeDocumentData document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.EmployeeId <= 0)
        {
            throw new InvalidOperationException(
                "A dokumentumból hiányzik az érvényes dolgozóazonosító.");
        }

        if (document.Header is null ||
            document.Sections is null ||
            document.Sections.Count == 0)
        {
            throw new InvalidOperationException(
                "A dokumentum fejléc- vagy adatrészlistája hiányos.");
        }

        foreach (var section in document.Sections)
        {
            if (section is null ||
                section.Fields is null ||
                section.Fields.Count == 0 ||
                section.OutputRows is null)
            {
                throw new InvalidOperationException(
                    "A dokumentum hiányos adatrészt tartalmaz.");
            }

            if (section.Fields.Count > 16_384)
            {
                throw new InvalidOperationException(
                    $"Túl sok kimeneti oszlop: {section.Title}");
            }

            var names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in section.Fields)
            {
                if (item is null ||
                    string.IsNullOrWhiteSpace(item.OutputName) ||
                    !names.Add(item.OutputName))
                {
                    throw new InvalidOperationException(
                        $"Hiányzó vagy ismétlődő oszlopnév: " +
                        section.Title);
                }
            }
        }
    }

    private static string FieldLabel(
        ReportFieldDefinition definition)
    {
        return string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.OutputName
            : definition.DisplayName;
    }

    private static string CreateUniqueWorksheetName(
        string? name,
        HashSet<string> usedNames)
    {
        var invalidCharacters = new[]
        {
            '[', ']', ':', '*', '?', '/', '\\'
        };

        var safeName = new string((name ?? "")
            .Select(character =>
                char.IsControl(character) ||
                invalidCharacters.Contains(character)
                    ? '_'
                    : character)
            .ToArray())
            .Trim()
            .Trim('\'');

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Adatok";
        }

        if (string.Equals(
                safeName,
                "History",
                StringComparison.OrdinalIgnoreCase))
        {
            safeName = "History_";
        }

        if (safeName.Length > 31)
        {
            safeName = safeName[..31];
        }

        var candidate = safeName;
        var index = 2;

        while (!usedNames.Add(candidate))
        {
            var suffix = $" ({index++})";

            var prefixLength = Math.Min(
                safeName.Length,
                31 - suffix.Length);

            candidate =
                safeName[..prefixLength] + suffix;
        }

        return candidate;
    }

    private static void SetCellValue(
        IXLCell cell,
        object? value)
    {
        switch (value)
        {
            case null:
                cell.Clear(XLClearOptions.Contents);
                break;

            case string text:
                cell.Value = text;
                break;

            case bool boolean:
                cell.Value = boolean;
                break;

            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;

            case DateTime dateTime:
                cell.Value = dateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;

            case DateTimeOffset dateTimeOffset:
                cell.Value = dateTimeOffset.ToString(
                    "O",
                    CultureInfo.InvariantCulture);
                break;

            case TimeOnly time:
                cell.Value = time.ToString(
                    "HH:mm:ss",
                    CultureInfo.InvariantCulture);
                break;

            case TimeSpan duration:
                cell.Value = duration.ToString(
                    "c",
                    CultureInfo.InvariantCulture);
                break;

            case decimal number:
                cell.Value = number;
                break;

            case byte number:
                cell.Value = number;
                break;

            case short number:
                cell.Value = number;
                break;

            case int number:
                cell.Value = number;
                break;

            case long number:
                cell.Value = number;
                break;

            case float number:
                cell.Value = number;
                break;

            case double number:
                cell.Value = number;
                break;

            case Guid guid:
                cell.Value = guid.ToString();
                break;

            case byte[] bytes:
                cell.Value = Convert.ToBase64String(bytes);
                break;

            default:
                cell.Value = FormatValue(value);
                break;
        }
    }

    private static string FormatValue(
        object? value,
        string? format = null)
    {
        if (value is null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(format) &&
            value is IFormattable formattable &&
            value is byte or short or int or long or
                float or double or decimal)
        {
            try
            {
                return formattable.ToString(
                    format,
                    DisplayCulture) ?? "";
            }
            catch (FormatException)
            {
            }
        }

        return value switch
        {
            string text => text,
            bool boolean => boolean ? "Igen" : "Nem",
            DateOnly date => date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString(
                "yyyy-MM-dd HH:mm:ss zzz",
                CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString(
                "HH:mm:ss",
                CultureInfo.InvariantCulture),
            TimeSpan duration => duration.ToString(
                "c",
                CultureInfo.InvariantCulture),
            Guid guid => guid.ToString(),
            byte[] bytes => Convert.ToBase64String(bytes),
            IFormattable number => number.ToString(
                null,
                DisplayCulture) ?? "",
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static IContainer HeaderCell(
        IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten2)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(5);
    }

    private static IContainer BodyCell(
        IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(5);
    }
}