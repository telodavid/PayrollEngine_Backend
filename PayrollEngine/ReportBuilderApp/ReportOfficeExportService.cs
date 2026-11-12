using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportOfficeExportService
{
    public byte[] CreateXlsx(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        using var workbook = new XLWorkbook();

        var worksheet = workbook.Worksheets.Add(
            CreateWorksheetName(report.Name));

        for (var columnIndex = 0;
             columnIndex < report.Fields.Count;
             columnIndex++)
        {
            var field = report.Fields[columnIndex];
            worksheet.Cell(1, columnIndex + 1).Value = field.OutputName;
        }

        var header = worksheet.Range(
            1,
            1,
            1,
            Math.Max(report.Fields.Count, 1));

        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0;
                 columnIndex < report.Fields.Count;
                 columnIndex++)
            {
                var field = report.Fields[columnIndex];
                rows[rowIndex].TryGetValue(field.OutputName, out var value);

                SetCellValue(
                    worksheet.Cell(rowIndex + 2, columnIndex + 1),
                    value);
            }
        }

        for (var columnIndex = 0;
             columnIndex < report.Fields.Count;
             columnIndex++)
        {
            var format = report.Fields[columnIndex].Format;

            if (!string.IsNullOrWhiteSpace(format))
            {
                worksheet.Column(columnIndex + 1)
                    .Style.NumberFormat.Format = format;
            }
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();

        if (report.Fields.Count > 0)
        {
            worksheet.Range(
                    1,
                    1,
                    Math.Max(rows.Count + 1, 1),
                    report.Fields.Count)
                .SetAutoFilter();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] CreatePdf(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header()
                    .PaddingBottom(10)
                    .Text(report.Name)
                    .SemiBold()
                    .FontSize(14);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var field in report.Fields)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var field in report.Fields)
                        {
                            header.Cell()
                                .Element(HeaderCell)
                                .Text(field.OutputName);
                        }
                    });

                    foreach (var row in rows)
                    {
                        foreach (var field in report.Fields)
                        {
                            row.TryGetValue(field.OutputName, out var value);

                            table.Cell()
                                .Element(BodyCell)
                                .Text(FormatValue(value));
                        }
                    }
                });

                page.Footer()
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

        return document.GeneratePdf();
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
                break;
            case decimal number:
                cell.Value = number;
                break;
            case int number:
                cell.Value = number;
                break;
            case long number:
                cell.Value = number;
                break;
            case double number:
                cell.Value = number;
                break;
            default:
                cell.Value = FormatValue(value);
                break;
        }
    }

    private static string CreateWorksheetName(string name)
    {
        var invalidCharacters = new[] { '[', ']', ':', '*', '?', '/', '\\' };
        var safeName = new string(name
            .Select(character =>
                invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(safeName))
        {
            return "Riport";
        }

        return safeName.Length <= 31
            ? safeName
            : safeName[..31];
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten2)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(4);
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4);
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "",
            DateOnly date => date.ToString("yyyy-MM-dd"),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            IFormattable formattable =>
                formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
    }
}