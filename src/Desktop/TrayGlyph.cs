using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AmuleModern.Desktop;

internal static class TrayGlyph
{
    public static WindowIcon Create()
    {
        const int size = 32;
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = bitmap.Lock())
        {
            var data = new byte[fb.RowBytes * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * fb.RowBytes + x * 4;
                int dx = x - 16, dy = y - 16, r2 = dx * dx + dy * dy;
                bool on = r2 is >= 40 and <= 180;
                data[i] = (byte)(on ? 0xC6 : 0x21);
                data[i + 1] = (byte)(on ? 0xDB : 0x15);
                data[i + 2] = (byte)(on ? 0x80 : 0x0D);
                data[i + 3] = 0xFF;
            }
            Marshal.Copy(data, 0, fb.Address, data.Length);
        }
        var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}
