using AmuleModern.Desktop;

int failed = 0;
Check(ColumnLayoutRules.Normalize([], ["name", "size"]).Select(c => c.Id).SequenceEqual(["name", "size"]), "missing layout keeps the declared order");

var reordered = ColumnLayoutRules.Normalize(
[
    new("size", 0, true, 180, "Pixel"),
    new("name", 1, false, 2, "Star"),
    new("gone", 2, true, 10, "Pixel"),
    new("size", 3, false, 90, "Pixel")
], ["name", "size", "speed"]);
Check(reordered.Select(c => c.Id).SequenceEqual(["size", "name", "speed"]), "unknown and duplicate columns are dropped and new ones go last");
Check(reordered[1].Visible && reordered[1].WidthUnit == "Star" && Math.Abs(reordered[1].WidthValue - 2) < 0.001, "the first column stays visible and keeps its star width");
Check(reordered[0].WidthUnit == "Pixel" && Math.Abs(reordered[0].WidthValue - 180) < 0.001, "pixel width is kept");
Check(reordered[2].Visible && reordered[2].WidthUnit == "", "a new column stays visible without overriding its default width");
Check(reordered.Select(c => c.DisplayIndex).SequenceEqual([0, 1, 2]), "display indexes are compacted");

var tiny = ColumnLayoutRules.Normalize([new("name", 0, true, 10, "Pixel")], ["name"]);
Check(tiny[0].WidthUnit == "Pixel" && Math.Abs(tiny[0].WidthValue - 48) < 0.001, "pixel width is clamped to the minimum");
var huge = ColumnLayoutRules.Normalize([new("name", 0, true, 9000, "Pixel")], ["name"]);
Check(huge[0].WidthValue == 4000, "pixel width is clamped to the maximum");
var junk = ColumnLayoutRules.Normalize([new("name", 0, true, 0, "Pixel"), new("size", 5, true, double.NaN, "Nope")], ["name", "size"]);
Check(junk[0].WidthUnit == "" && junk[1].WidthUnit == "", "invalid widths do not replace the column default");

string dir = Path.Combine(Path.GetTempPath(), "amule-layout-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
UiSettings.FilePathOverride = Path.Combine(dir, "ui.json");
try
{
    File.WriteAllText(UiSettings.Path, """{"theme":"System"}""");
    Check(UiSettings.LoadTheme() == "System" && UiSettings.LoadDensity() == "Comfortable", "an older theme file still loads");
    UiSettings.SaveColumns("downloads", reordered);
    UiSettings.SaveDensity("Compact");
    UiSettings.SaveTheme("Light");
    Check(UiSettings.LoadTheme() == "Light" && UiSettings.LoadDensity() == "Compact", "theme, density and columns share one file");
    var loaded = UiSettings.LoadColumns("downloads", ["name", "size", "speed"]);
    Check(loaded.Select(c => $"{c.Id}:{c.Visible}:{c.WidthUnit}:{c.WidthValue}").SequenceEqual(reordered.Select(c => $"{c.Id}:{c.Visible}:{c.WidthUnit}:{c.WidthValue}")),
        "column layout roundtrip");
    var servers = UiSettings.LoadColumns("servers", ["name", "address"]);
    Check(servers.Select(c => c.Id).SequenceEqual(["name", "address"]) && UiSettings.LoadTheme() == "Light", "another table does not erase the theme or invent columns");
    File.WriteAllText(UiSettings.Path, "{");
    Check(UiSettings.LoadTheme() == "Dark" && UiSettings.LoadColumns("downloads", ["name"]).Single().Id == "name", "a broken file falls back instead of throwing");
}
finally
{
    UiSettings.FilePathOverride = null;
    Directory.Delete(dir, true);
}

if (failed > 0)
{
    Console.Error.WriteLine("RESULT: " + failed + " layout checks failed.");
    return 1;
}
Console.WriteLine("RESULT: layout checks passed.");
return 0;

void Check(bool condition, string name)
{
    Console.WriteLine((condition ? "PASS: " : "FAIL: ") + name);
    if (!condition) failed++;
}
