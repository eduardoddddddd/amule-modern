using Avalonia;

namespace AmuleModern.Desktop;

internal static class Program
{
    public static string? CapturePath { get; private set; }
    public static bool ExerciseUi { get; private set; }
    public static bool CaptureFailed { get; set; }
    [STAThread]
    public static int Main(string[] args)
    {
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && capture + 1 < args.Length) CapturePath = Path.GetFullPath(args[capture + 1]);
        ExerciseUi = CapturePath != null && args.Contains("--exercise-ui");
        int exitCode = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return CaptureFailed ? 1 : exitCode;
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
