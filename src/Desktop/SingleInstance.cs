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
        string name = @"Local\AmuleModern." + profile;
        mutex = new Mutex(true, name, out bool created);
        show = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".show");
        if (created) return true;
        if (!string.IsNullOrWhiteSpace(pendingLink))
        {
            Directory.CreateDirectory(PendingDirectory());
            File.WriteAllText(PendingFile(), pendingLink.Trim());
        }
        show.Set();
        mutex.Dispose();
        show.Dispose();
        return false;
    }
    public static void Watch(Action<string?> onShow, CancellationToken token)
    {
        if (show is null) return;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                if (show.WaitOne(TimeSpan.FromMilliseconds(400)))
                {
                    string? link = null;
                    string file = PendingFile();
                    if (File.Exists(file))
                    {
                        link = File.ReadAllText(file).Trim();
                        File.Delete(file);
                    }
                    onShow(string.IsNullOrWhiteSpace(link) ? null : link);
                }
            }
        }, token);
    }
    public static void SignalShow() => show?.Set();
    private static string PendingDirectory()
    {
        var repo = AmuleModern.Amule.EngineSession.FindRepository();
        return Path.Combine(repo, ".local", Profile);
    }
    private static string PendingFile() => Path.Combine(PendingDirectory(), "pending-ed2k.txt");
}
