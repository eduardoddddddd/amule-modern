using System.Runtime.InteropServices;

namespace AmuleModern.Amule;

public static class UserFolders
{
    public const string LibraryName = "amule-modern";
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    public static bool IsIsolatedProfile(string profileName) =>
        profileName.StartsWith("integration", StringComparison.Ordinal) || profileName == "capture";

    public static string DownloadsRoot()
    {
        if (OperatingSystem.IsWindows() && NativeDownloads(out string? known) && !string.IsNullOrWhiteSpace(known))
            return known;
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string downloads = Path.Combine(home, "Downloads");
        return Directory.Exists(downloads) ? downloads : home;
    }

    public static string LibraryRoot() => Path.Combine(DownloadsRoot(), LibraryName);
    public static string Incoming() => Path.Combine(LibraryRoot(), "incoming");
    public static string Temp() => Path.Combine(LibraryRoot(), "tmp");
    public static string ForConfig(string path) => Path.GetFullPath(path).Replace('\\', '/');
    public static string FromConfig(string path) => Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));

    public static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    public static bool IsUnder(string path, string parent) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || PathsEqual(path, parent);

    public static void MigrateLegacyProfileOnce(string profile, string incoming, string temp, bool migrateIncoming, bool migrateTemp)
    {
        string marker = Path.Combine(profile, "folders-migrated-v1");
        if (File.Exists(marker)) return;
        // An external path in an existing config means the previous version already
        // migrated it. Never restore old backups into that active library again.
        if (migrateIncoming) CopyContents(Path.Combine(profile, "Incoming"), incoming);
        if (migrateTemp) CopyContents(Path.Combine(profile, "Temp"), temp);
        File.WriteAllText(marker, "Legacy folders processed. Originals retained as backup.\n");
    }
    private static void CopyContents(string from, string to)
    {
        if (!Directory.Exists(from) || PathsEqual(from, to)) return;
        Directory.CreateDirectory(to);
        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string dest = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (File.Exists(dest))
            {
                if (!FilesEqual(file, dest)) throw new IOException("La migración encontró un archivo distinto en destino: " + dest);
                continue;
            }
            string staging = dest + ".migrating-" + Guid.NewGuid().ToString("N");
            try { File.Copy(file, staging); File.Move(staging, dest); }
            finally { if (File.Exists(staging)) File.Delete(staging); }
        }
    }
    private static bool FilesEqual(string left, string right)
    {
        using var a = File.OpenRead(left);
        using var b = File.OpenRead(right);
        if (a.Length != b.Length) return false;
        byte[] first = new byte[65536], second = new byte[65536];
        while (a.Position < a.Length)
        {
            int count = (int)Math.Min(first.Length, a.Length - a.Position);
            a.ReadExactly(first.AsSpan(0, count)); b.ReadExactly(second.AsSpan(0, count));
            if (!first.AsSpan(0, count).SequenceEqual(second.AsSpan(0, count))) return false;
        }
        return true;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid rfid, uint flags, nint token, out nint path);

    private static bool NativeDownloads(out string? path)
    {
        path = null;
        if (SHGetKnownFolderPath(DownloadsFolderId, 0, 0, out nint buffer) != 0) return false;
        try { path = Marshal.PtrToStringUni(buffer); return true; }
        finally { Marshal.FreeCoTaskMem(buffer); }
    }
}
