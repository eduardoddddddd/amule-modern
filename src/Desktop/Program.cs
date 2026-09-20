using Avalonia;

namespace AmuleModern.Desktop;

internal static class Program
{
    public static string? CapturePath { get; private set; }
    public static bool ExerciseUi { get; private set; }
    public static bool ShowServers { get; private set; }
    public static bool ShowSearch { get; private set; }
    public static bool ShowSettings { get; private set; }
    public static bool ShowShared { get; private set; }
    public static string? ConnectServer { get; private set; }
    public static string? StartupLink { get; private set; }
    public static bool CaptureFailed { get; set; }
    [STAThread]
    public static int Main(string[] args)
    {
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && capture + 1 < args.Length) CapturePath = Path.GetFullPath(args[capture + 1]);
        ExerciseUi = CapturePath != null && args.Contains("--exercise-ui");
        ShowServers = args.Contains("--servers");
        ShowSearch = args.Contains("--search");
        ShowSettings = args.Contains("--settings");
        ShowShared = args.Contains("--shared");
        int server = Array.IndexOf(args, "--connect-server");
        if (server >= 0 && server + 1 < args.Length) ConnectServer = args[server + 1];
        string? ed2k = args.FirstOrDefault(a => a.StartsWith("ed2k://", StringComparison.OrdinalIgnoreCase));
        if (CapturePath == null && !SingleInstance.TryClaim("desktop", ed2k)) return 0;
        StartupLink = ed2k;
        int exitCode = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return CaptureFailed ? 1 : exitCode;
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
