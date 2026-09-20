using System.Text.Json;

namespace AmuleModern.Desktop;

internal static class UiSettings
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "amule-modern", "ui.json");

    public static string LoadTheme()
    {
        try
        {
            if (!File.Exists(Path)) return "Dark";
            using var stream = File.OpenRead(Path);
            var doc = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            string? theme = doc?.GetValueOrDefault("theme");
            return theme is "Light" or "Dark" or "System" ? theme : "Dark";
        }
        catch { return "Dark"; }
    }

    public static void SaveTheme(string theme)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(new Dictionary<string, string> { ["theme"] = theme }, Json));
        }
        catch { /* preferencias de UI no bloquean el motor */ }
    }
}
