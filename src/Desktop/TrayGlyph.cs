using Avalonia.Controls;
using Avalonia.Platform;

namespace AmuleModern.Desktop;

internal static class TrayGlyph
{
    public static WindowIcon Create()
    {
        using var stream = AssetLoader.Open(new Uri("avares://AmuleModern/Assets/amule-modern.ico"));
        return new WindowIcon(stream);
    }
}
