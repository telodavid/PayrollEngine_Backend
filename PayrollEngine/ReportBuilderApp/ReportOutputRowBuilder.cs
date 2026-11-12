using System.Globalization;
using System.Text.Json;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportOutputRowBuilder
{
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Build(
        ReportDefinition report,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> sourceRows)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(sourceRows);

        if (report.Fields.Count == 0)
        {
            throw new InvalidOperationException(
                "A riporthoz legalább egy kimeneti mezőt meg kell adni.");
        }

        var rowMode = report.Grouping.RowMode.Trim();

        if (string.Equals(
                rowMode,
                "DETAIL",
                StringComparison.OrdinalIgnoreCase))
        {
            return sourceRows
                .Select(row => BuildDetailRow(report, row))
                .ToArray();
        }

        if (string.Equals(
                rowMode,
                "GROUPED",
                StringComparison.OrdinalIgnoreCase))
        {
            return BuildGroupedRows(report, sourceRows);
        }

        throw new InvalidOperationException(
            $"Ismeretlen riport-sormód: {report.Grouping.RowMode}");
    }

    private static IReadOnlyDictionary<string, object?> BuildDetailRow(
        ReportDefinition report,
        IReadOnlyDictionary<string, object?> sourceRow)
    {
        var output = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var field in report.Fields)
        {
            output[field.OutputName] = ResolveValue(field.Value, sourceRow);
        }

        return output;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>>
        BuildGroupedRows(
            ReportDefinition report,
            IReadOnlyList<IReadOnlyDictionary<string, object?>> sourceRows)
    {
        if (report.Grouping.GroupBy.Count == 0)
        {
            throw new InvalidOperationException(
                "Csoportosított riportnál legalább egy csoportosító mező szükséges.");
        }

        var groups = sourceRows.GroupBy(
            row => CreateGroupKey(row, report.Grouping.GroupBy),
            StringComparer.Ordinal);

        var outputRows =
            new List<IReadOnlyDictionary<string, object?>>();

        foreach (var group in groups)
        {
            var rows = group.ToArray();
            var firstRow = rows[0];
            var output = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var field in report.Fields)
            {
                output[field.OutputName] = ResolveGroupedValue(
                    field,
                    rows,
                    firstRow);
            }

            outputRows.Add(output);
        }

        return outputRows;
    }

    private static object? ResolveGroupedValue(
        ReportFieldDefinition field,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IReadOnlyDictionary<string, object?> firstRow)
    {
        var aggregation = field.Aggregation.Trim().ToUpperInvariant();

        if (aggregation == "NONE")
        {
            return ResolveValue(field.Value, firstRow);
        }

        if (!string.Equals(
                field.Value.Kind,
                "REFERENCE",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"A(z) {field.OutputName} mező összesítése csak adat-hivatkozáson használható.");
        }

        var values = rows
            .Select(row => ResolveValue(field.Value, row))
            .Where(value => value is not null)
            .ToArray();

        return aggregation switch
        {
            "COUNT" => values.Length,
            "SUM" => Sum(values, field.OutputName),
            "MIN" => FindExtreme(values, field.OutputName, findMinimum: true),
            "MAX" => FindExtreme(values, field.OutputName, findMinimum: false),
            _ => throw new InvalidOperationException(
                $"Ismeretlen összesítési mód: {field.Aggregation}")
        };
    }

    private static object? ResolveValue(
        ReportValueDefinition definition,
        IReadOnlyDictionary<string, object?> sourceRow)
    {
        if (string.Equals(
                definition.Kind,
                "CONSTANT",
                StringComparison.OrdinalIgnoreCase))
        {
            return definition.Constant is { } constant
                ? ReadJsonValue(constant)
                : null;
        }

        if (!string.Equals(
                definition.Kind,
                "REFERENCE",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Ismeretlen riportérték-típus: {definition.Kind}");
        }

        if (string.IsNullOrWhiteSpace(definition.Reference))
        {
            throw new InvalidOperationException(
                "A riport adat-hivatkozása hiányzik.");
        }

        if (sourceRow.TryGetValue(definition.Reference, out var value))
        {
            return value;
        }

        var match = sourceRow.FirstOrDefault(item =>
            string.Equals(
                item.Key,
                definition.Reference,
                StringComparison.OrdinalIgnoreCase));

        if (match.Key is not null)
        {
            return match.Value;
        }

        throw new KeyNotFoundException(
            $"A riport adat-hivatkozása nem található a betöltött adatok között: {definition.Reference}");
    }

    private static object? ReadJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.Array => value.EnumerateArray()
                .Select(ReadJsonValue)
                .ToArray(),
            JsonValueKind.Object => value.GetRawText(),
            _ => value.ToString()
        };
    }

    private static string CreateGroupKey(
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<string> references)
    {
        var values = references
            .Select(reference => FindValue(row, reference))
            .Select(FormatKeyValue)
            .ToArray();

        return JsonSerializer.Serialize(values);
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

        if (match.Key is not null)
        {
            return match.Value;
        }

        throw new KeyNotFoundException(
            $"A csoportosító adat-hivatkozás nem található: {reference}");
    }

    private static string? FormatKeyValue(object? value)
    {
        return value switch
        {
            null => null,
            DateOnly date => date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static decimal Sum(
        IReadOnlyList<object?> values,
        string outputName)
    {
        decimal total = 0;

        foreach (var value in values)
        {
            try
            {
                total += Convert.ToDecimal(
                    value,
                    CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"A(z) {outputName} mező értéke nem összegezhető.",
                    exception);
            }
        }

        return total;
    }

    private static object FindExtreme(
        IReadOnlyList<object?> values,
        string outputName,
        bool findMinimum)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                $"A(z) {outputName} mezőhöz nincs összesíthető érték.");
        }

        var result = values[0]!;

        foreach (var value in values.Skip(1))
        {
            if (value is not IComparable comparable)
            {
                throw new InvalidOperationException(
                    $"A(z) {outputName} mező értékei nem hasonlíthatók össze.");
            }

            var comparison = comparable.CompareTo(result);

            if ((findMinimum && comparison < 0) ||
                (!findMinimum && comparison > 0))
            {
                result = value!;
            }
        }

        return result;
    }
}