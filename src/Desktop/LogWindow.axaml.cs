using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class LogWindow : UserControl
{
    private readonly EcClient client = null!;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool available, closed;
    public LogWindow()
    {
        InitializeComponent();
    }
    public LogWindow(EcClient client) : this()
    {
        this.client = client;
        AttachedToVisualTree += async (_, _) =>
        {
            closed = false;
            available = true;
            RefreshButton.IsEnabled = CopyButton.IsEnabled = true;
            await ReloadAsync();
            if (!closed) timer.Start();
        };
        DetachedFromVisualTree += (_, _) => { timer.Stop(); closed = true; };
        timer.Tick += async (_, _) =>
        {
            if (!available || closed || !await gate.WaitAsync(0)) return;
            try { await ReloadAsync(); }
            finally { gate.Release(); }
        };
    }
    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await ReloadAsync();
    private async Task ReloadAsync()
    {
        if (closed) return;
        try
        {
            string activity = await client.GetActivityLogAsync();
            string servers = await client.GetServerLogAsync();
            string text = activity.TrimEnd() + "\n\n--- mensajes del servidor ---\n" + servers.TrimEnd();
            if (LogBox.Text != text)
            {
                LogBox.Text = text;
                LogBox.CaretIndex = text.Length;
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
