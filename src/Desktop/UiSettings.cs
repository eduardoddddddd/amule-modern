using System.Text.Json;
using System.Text.Json.Nodes;

namespace AmuleModern.Desktop;

internal static class UiSettings
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static string? FilePathOverride { get; set; }
    public static string Path => FilePathOverride ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "amule-modern", "ui.json");

    public static string LoadTheme()
    {
        lock (Gate)
        {
            string? theme = Text(ReadObject(), "theme");
            return theme is "Light" or "Dark" or "System" ? theme : "Dark";
        }
    }

    public static void SaveTheme(string theme)
    {
        lock (Gate)
        {
            var root = ReadObject();
            root["theme"] = theme is "Light" or "Dark" or "System" ? theme : "Dark";
            WriteObject(root);
        }
    }

    public static string LoadDensity()
    {
        lock (Gate)
        {
            string? density = Text(ReadObject(), "density");
            return density == "Compact" ? "Compact" : "Comfortable";
        }
    }

    public static void SaveDensity(string density)
    {
        lock (Gate)
        {
            var root = ReadObject();
            root["density"] = density == "Compact" ? "Compact" : "Comfortable";
            WriteObject(root);
        }
    }

    public static IReadOnlyList<ColumnLayout> LoadColumns(string table, IReadOnlyList<string> ids)
    {
        lock (Gate)
        {
            var saved = ColumnLayoutRules.FromJsonArray(ReadObject()["columns"]?[table]);
            return ColumnLayoutRules.Normalize(saved, ids);
        }
    }

    public static void SaveColumns(string table, IReadOnlyList<ColumnLayout> columns)
    {
        lock (Gate)
        {
            var root = ReadObject();
            if (root["columns"] is not JsonObject tables)
            {
                tables = new JsonObject();
                root["columns"] = tables;
            }
            tables[table] = ColumnLayoutRules.ToJsonArray(columns);
            WriteObject(root);
        }
    }

    private static JsonObject ReadObject()
    {
        try
        {
            if (!File.Exists(Path)) return new JsonObject();
            return JsonNode.Parse(File.ReadAllText(Path)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException) { return new JsonObject(); }
        catch (IOException) { return new JsonObject(); }
    }

    private static void WriteObject(JsonObject root)
    {
        try
        {
            string path = Path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, root.ToJsonString(Json));
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException) { /* preferencias de UI no bloquean el motor */ }
        catch (UnauthorizedAccessException) { }
    }

    private static string? Text(JsonObject obj, string name)
    {
        try { return obj[name]?.GetValue<string>(); }
        catch (InvalidOperationException) { return null; }
        catch (FormatException) { return null; }
    }
}
