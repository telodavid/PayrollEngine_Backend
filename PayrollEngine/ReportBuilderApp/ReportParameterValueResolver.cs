using System.Globalization;
using System.Text.Json;
using PayrollEngine.ReportBuilderApp.Models;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportParameterValueResolver
{
    public IReadOnlyDictionary<string, JsonElement> Resolve(
        ReportDefinition report,
        ReportExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(request);

        var definitions = new Dictionary<string, ReportParameterDefinition>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var definition in report.Parameters)
        {
            if (definition is null ||
                string.IsNullOrWhiteSpace(definition.Code))
            {
                throw new InvalidOperationException(
                    "A riport paraméterdefiníciójából hiányzik a kód.");
            }

            if (!definitions.TryAdd(definition.Code, definition))
            {
                throw new InvalidOperationException(
                    $"Ismétlődő riportparaméter: {definition.Code}");
            }
        }

        var supplied = new Dictionary<string, JsonElement>(
            StringComparer.OrdinalIgnoreCase);

        if (request.ParameterValues is not null)
        {
            foreach (var parameter in request.ParameterValues)
            {
                if (!definitions.ContainsKey(parameter.Key))
                {
                    throw new ArgumentException(
                        $"Ismeretlen riportparaméter: {parameter.Key}");
                }

                if (!supplied.TryAdd(parameter.Key, parameter.Value))
                {
                    throw new ArgumentException(
                        $"Ismétlődő paraméterérték: {parameter.Key}");
                }
            }
        }

        var result = new Dictionary<string, JsonElement>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions.Values)
        {
            JsonElement? value;

            if (supplied.TryGetValue(definition.Code, out var suppliedValue))
            {
                value = suppliedValue;
            }
            else
            {
                value = ResolveModeValue(definition.Mode, request);

                if (value is null)
                {
                    value = definition.DefaultValue;
                }
            }

            if (IsMissing(value))
            {
                if (definition.Required)
                {
                    var label = string.IsNullOrWhiteSpace(definition.Label)
                        ? definition.Code
                        : definition.Label;

                    throw new ArgumentException(
                        $"A következő paraméter megadása kötelező: {label}");
                }

                continue;
            }

            var resolved = value!.Value;

            ValidateType(definition, resolved);

            result.Add(definition.Code, resolved.Clone());
        }

        return result;
    }

    private static JsonElement? ResolveModeValue(
        string? mode,
        ReportExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return null;
        }

        return mode.Trim().ToUpperInvariant() switch
        {
            "AS_OF_DATE" => SerializeDate(request.AsOfDate),
            "DATE_RANGE_FROM" => SerializeDate(request.DateFrom),
            "DATE_RANGE_TO" => SerializeDate(request.DateTo),
            "MONTH_RANGE_FROM" => SerializeText(request.PeriodFrom),
            "MONTH_RANGE_TO" => SerializeText(request.PeriodTo),
            _ => throw new InvalidOperationException(
                $"Ismeretlen paramétermód: {mode}")
        };
    }

    private static JsonElement? SerializeDate(DateOnly? value)
    {
        return value.HasValue
            ? JsonSerializer.SerializeToElement(
                value.Value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture))
            : null;
    }

    private static JsonElement? SerializeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : JsonSerializer.SerializeToElement(value);
    }

    private static bool IsMissing(JsonElement? value)
    {
        if (value is null)
        {
            return true;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => true,
            JsonValueKind.String =>
                string.IsNullOrWhiteSpace(value.Value.GetString()),
            _ => false
        };
    }

    private static void ValidateType(
        ReportParameterDefinition definition,
        JsonElement value)
    {
        var type = definition.Type?.Trim().ToLowerInvariant();

        var valid = type switch
        {
            "text" or "option" =>
                value.ValueKind == JsonValueKind.String,

            "integer" =>
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out _),

            "decimal" or "number" =>
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDecimal(out _),

            "boolean" =>
                value.ValueKind is
                    JsonValueKind.True or JsonValueKind.False,

            "date" =>
                IsDate(value),

            "month" =>
                IsMonth(value),

            "time" =>
                IsTime(value),

            "guid" =>
                value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(value.GetString(), out _),

            "collection" =>
                value.ValueKind == JsonValueKind.Array,

            _ => throw new InvalidOperationException(
                $"Ismeretlen paraméter-adattípus: " +
                $"{definition.Code}/{definition.Type}")
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"A paraméter értéke nem felel meg az elvárt típusnak: " +
                $"{definition.Code} ({definition.Type})");
        }
    }

    private static bool IsDate(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String &&
               DateOnly.TryParseExact(
                   value.GetString(),
                   "yyyy-MM-dd",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out _);
    }

    private static bool IsMonth(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = value.GetString();

        return text is { Length: 7 } &&
               DateOnly.TryParseExact(
                   $"{text}-01",
                   "yyyy-MM-dd",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out _);
    }

    private static bool IsTime(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String &&
               TimeOnly.TryParseExact(
                   value.GetString(),
                   ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF"],
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out _);
    }
}