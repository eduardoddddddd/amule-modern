using System.Threading;

namespace AmuleModern.Desktop;

internal static class SingleInstance
{
    private static Mutex? mutex;
    private static EventWaitHandle? show;
    public static string Profile { get; private set; } = "desktop";
    public static bool TryClaim(string profile, string? pendingLink)
    {
        Profile = profile;
        string name = (OperatingSystem.IsWindows() ? @"Local\AmuleModern." : "AmuleModern.") + profile;
        mutex = new Mutex(true, name, out bool created);
        if (OperatingSystem.IsWindows())
            show = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".show");
        if (created) return true;
        if (!string.IsNullOrWhiteSpace(pendingLink))
        {
            Directory.CreateDirectory(PendingDirectory());
            File.WriteAllText(PendingFile(), pendingLink.Trim());
        }
        SignalShow();
        mutex.Dispose();
        show?.Dispose();
        return false;
    }
    public static void Watch(Action<string?> onShow, CancellationToken token)
    {
        if (OperatingSystem.IsWindows() && show is null) return;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                bool signaled = show?.WaitOne(TimeSpan.FromMilliseconds(400)) ?? ConsumeShowFile();
                if (!signaled)
                {
                    if (show is null) Thread.Sleep(400);
                    continue;
                }
                onShow(TakePendingLink());
            }
        }, token);
    }
    public static void SignalShow()
    {
        if (show != null)
        {
            show.Set();
            return;
        }
        Directory.CreateDirectory(PendingDirectory());
        File.WriteAllText(ShowFile(), "1");
    }
    private static bool ConsumeShowFile()
    {
        string file = ShowFile();
        if (!File.Exists(file)) return false;
        File.Delete(file);
        return true;
    }
    private static string? TakePendingLink()
    {
        string file = PendingFile();
        if (!File.Exists(file)) return null;
        string link = File.ReadAllText(file).Trim();
        File.Delete(file);
        return string.IsNullOrWhiteSpace(link) ? null : link;
    }
    private static string PendingDirectory()
    {
        var repo = AmuleModern.Amule.EngineSession.FindRepository();
        return Path.Combine(repo, ".local", Profile);
    }
    private static string PendingFile() => Path.Combine(PendingDirectory(), "pending-ed2k.txt");
    private static string ShowFile() => Path.Combine(PendingDirectory(), "show.request");
}
