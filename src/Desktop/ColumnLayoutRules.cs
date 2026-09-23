using System.Text.Json.Nodes;

namespace AmuleModern.Desktop;

internal sealed record ColumnLayout(string Id, int DisplayIndex, bool Visible, double WidthValue, string WidthUnit);

// Pure rules for remembering DataGrid columns. The first id is the column that stays visible.
internal static class ColumnLayoutRules
{
    public static IReadOnlyList<ColumnLayout> Normalize(IReadOnlyList<ColumnLayout> saved, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0) throw new ArgumentException("La tabla no tiene columnas.");
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw new ArgumentException("Identificadores de columna duplicados o vacíos.");

        var known = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++) known[ids[i]] = i;

        var chosen = new List<ColumnLayout>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in saved.OrderBy(s => s.DisplayIndex).ThenBy(s => known.GetValueOrDefault(s.Id, int.MaxValue)))
        {
            if (!known.ContainsKey(item.Id) || !seen.Add(item.Id)) continue;
            chosen.Add(Sanitize(item));
        }
        foreach (string id in ids)
        {
            if (seen.Contains(id)) continue;
            chosen.Add(new ColumnLayout(id, chosen.Count, true, double.NaN, ""));
        }

        string required = ids[0];
        var result = new List<ColumnLayout>(chosen.Count);
        for (int i = 0; i < chosen.Count; i++)
        {
            var item = chosen[i];
            result.Add(item with { DisplayIndex = i, Visible = item.Id == required || item.Visible });
        }
        return result;
    }

    public static JsonArray ToJsonArray(IReadOnlyList<ColumnLayout> columns)
    {
        var array = new JsonArray();
        foreach (var column in columns.OrderBy(c => c.DisplayIndex))
        {
            var obj = new JsonObject
            {
                ["id"] = column.Id,
                ["index"] = column.DisplayIndex,
                ["visible"] = column.Visible
            };
            if (column.WidthUnit is "Pixel" or "Star" or "Auto" or "SizeToCells" or "SizeToHeader")
            {
                obj["unit"] = column.WidthUnit;
                if (column.WidthUnit is "Pixel" or "Star" && !double.IsNaN(column.WidthValue) && !double.IsInfinity(column.WidthValue))
                    obj["width"] = column.WidthValue;
            }
            array.Add(obj);
        }
        return array;
    }

    public static IReadOnlyList<ColumnLayout> FromJsonArray(JsonNode? node)
    {
        if (node is not JsonArray array) return [];
        var list = new List<ColumnLayout>();
        foreach (var item in array)
        {
            if (item is not JsonObject obj) continue;
            string? id = Text(obj, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            int index = Number(obj["index"], list.Count);
            bool visible = obj["visible"] is JsonValue flag && flag.TryGetValue<bool>(out bool shown) ? shown : true;
            string unit = Text(obj, "unit") ?? "";
            double width = NumberOrNaN(obj["width"]);
            list.Add(new ColumnLayout(id, index, visible, width, unit));
        }
        return list;
    }

    private static ColumnLayout Sanitize(ColumnLayout item)
    {
        string unit = item.WidthUnit switch
        {
            "Pixel" when item.WidthValue is >= 1 and <= 100000 => "Pixel",
            "Star" when item.WidthValue is >= 0.1 and <= 100 => "Star",
            "Auto" or "SizeToCells" or "SizeToHeader" => item.WidthUnit,
            _ => ""
        };
        double value = unit switch
        {
            "Pixel" => Math.Clamp(item.WidthValue, 48, 4000),
            "Star" => Math.Clamp(item.WidthValue, 0.2, 12),
            _ => double.NaN
        };
        return item with { WidthUnit = unit, WidthValue = value };
    }

    private static string? Text(JsonObject obj, string name)
    {
        try { return obj[name]?.GetValue<string>(); }
        catch (InvalidOperationException) { return null; }
        catch (FormatException) { return null; }
    }

    private static int Number(JsonNode? node, int fallback)
    {
        double value = NumberOrNaN(node);
        if (double.IsNaN(value) || value < 0 || value > 1000) return fallback;
        return (int)value;
    }

    private static double NumberOrNaN(JsonNode? node)
    {
        if (node is not JsonValue value) return double.NaN;
        if (value.TryGetValue<double>(out double number)) return number;
        if (value.TryGetValue<int>(out int integer)) return integer;
        return double.NaN;
    }
}
