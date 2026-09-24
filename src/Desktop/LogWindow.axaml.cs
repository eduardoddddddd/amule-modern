using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class LogWindow : UserControl
{
    private const int MaxShownChars = 256 * 1024;
    private readonly Func<EcClient> clientSource = null!;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool closed;
    public LogWindow()
    {
        InitializeComponent();
    }
    public LogWindow(Func<EcClient> clientSource) : this()
    {
        this.clientSource = clientSource;
        AttachedToVisualTree += async (_, _) =>
        {
            closed = false;
            RefreshButton.IsEnabled = CopyButton.IsEnabled = true;
            await ReloadGuardedAsync();
            if (!closed) timer.Start();
        };
        DetachedFromVisualTree += (_, _) => { timer.Stop(); closed = true; };
        timer.Tick += async (_, _) => await ReloadGuardedAsync();
    }
    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await ReloadGuardedAsync();
    private async Task ReloadGuardedAsync()
    {
        if (closed || !await gate.WaitAsync(0)) return;
        try { await ReloadAsync(); }
        finally { gate.Release(); }
    }
    private async Task ReloadAsync()
    {
        if (closed) return;
        try
        {
            var client = clientSource();
            string activity = await client.GetActivityLogAsync();
            string servers = await client.GetServerLogAsync();
            string text = activity.TrimEnd() + "\n\n--- mensajes del servidor ---\n" + servers.TrimEnd();
            if (text.Length > MaxShownChars) text = "…\n" + text[^MaxShownChars..];
            string shown = LogBox.Text ?? "";
            if (shown != text)
            {
                bool following = LogBox.CaretIndex >= shown.Length;
                int caret = LogBox.CaretIndex;
                LogBox.Text = text;
                LogBox.CaretIndex = following ? text.Length : Math.Min(caret, text.Length);
            }
            LogStatus.Text = "Registro del motor, en vivo.";
            CopyButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            LogStatus.Text = ex.Message;
        }
    }
    private async void CopyLog(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) { LogStatus.Text = "No hay portapapeles en esta sesión."; return; }
        await clipboard.SetTextAsync(LogBox.Text ?? "");
        LogStatus.Text = "Registro copiado.";
    }
}
