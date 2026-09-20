using System.Collections.ObjectModel;
using System.Diagnostics;
using AmuleModern.Amule;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DownloadItem> rows = [];
    private IReadOnlyList<DownloadItem> snapshot = [];
    private readonly EngineSession engine = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool ready, allowClose, closing;
    private string repository = "";

    public MainWindow()
    {
        InitializeComponent(); DownloadsGrid.ItemsSource = rows;
        Opened += WindowOpened;
        Closing += WindowClosing;
        timer.Tick += async (_, _) => await RefreshAsync();
    }
    private async void WindowOpened(object? sender, EventArgs e)
    {
        await operations.WaitAsync();
        try
        {
            repository = EngineSession.FindRepository();
            await engine.StartAsync(repository, Program.CapturePath == null ? "desktop" : "capture", lifetime.Token);
            ready = true;
            EngineBadge.Text = $"●  aMule {engine.Client.ServerVersion}";
            ConnectionStatus.Text = "Motor autenticado · consultando las redes…";
            Message.Text = "Perfil aislado listo. Añadir, pausar y reanudar ya funcionan. Al cerrar esta versión, el motor se detiene ordenadamente.";
            AddButton.IsEnabled = RefreshButton.IsEnabled = ServersButton.IsEnabled = true;
        }
        catch (Exception ex) { ShowError(ex); }
        finally { operations.Release(); }
        if (ready) { await RefreshAsync(); timer.Start(); }
        if (Program.CapturePath != null)
        {
            ServersWindow? serversWindow = null;
            try
            {
                if (!ready) throw new InvalidOperationException("La captura no pudo conectar al motor.");
                if (Program.ShowServers)
                {
                    serversWindow = new ServersWindow(engine.Client);
                    serversWindow.Show(this);
                    if (Program.ExerciseUi) await serversWindow.ExerciseUiAsync();
                }
                else if (Program.ExerciseUi) await ExerciseUiAsync();
            }
            catch (Exception ex) { Program.CaptureFailed = true; Message.Text = "Prueba visual fallida: " + ex.Message; }
            await Task.Delay(1000);
            Window target = serversWindow ?? (Window)this;
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)target.Bounds.Width, (int)target.Bounds.Height), new Vector(96, 96));
            bitmap.Render(target);
            Directory.CreateDirectory(Path.GetDirectoryName(Program.CapturePath)!);
            bitmap.Save(Program.CapturePath, PngBitmapEncoderOptions.Default);
            if (serversWindow != null)
            {
                File.WriteAllText(Path.ChangeExtension(Program.CapturePath, ".validation.txt"), Program.CaptureFailed ? "FAIL: servers UI" : "PASS: servers UI validation, add, selection, duplicate, disconnected state. Real engine.");
                serversWindow.Close();
            }
            Close();
        }
        else if (ready && Program.ShowServers) OpenServers(this, new RoutedEventArgs());
    }
    private async Task ExerciseUiAsync()
    {
        // Exercise actual routed button events, against the isolated capture engine.
        LinkInput.Text = "ed2k://|file|Prueba de interfaz - abc.txt|3|A448017AAF21D8525FC10AE87AA6729D|/";
        AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        if (rows.Count != 1) throw new InvalidOperationException("El botón Añadir no actualizó la tabla.");
        DownloadsGrid.SelectedItem = rows[0];
        PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        if (rows[0].State != 7) throw new InvalidOperationException("El botón Pausar no pausó la cola real.");
        ResumeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        if (rows[0].State == 7) throw new InvalidOperationException("El botón Reanudar no reanudó la cola real.");
        PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        FilterInput.Text = "no-coincide-123";
        await WaitForUiAsync(() => rows.Count == 0, "El filtro no excluye filas.");
        FilterInput.Text = "";
        await WaitForUiAsync(() => rows.Count == 1, "El filtro no restaura filas.");
        Message.Text = "Prueba de interfaz superada: añadir, pausar, reanudar y filtrar sobre el motor real. Archivo de prueba sin contenido descargado.";
        File.WriteAllText(Path.ChangeExtension(Program.CapturePath!, ".validation.txt"), "PASS: UI add, pause, resume, filter. Real EC engine; isolated fixture; no P2P transfer.\n");
    }
    private static async Task WaitForUiAsync(Func<bool> condition, string error)
    {
        // TextChanged is delivered through the dispatcher after the property assignment.
        for (int i = 0; i < 100; i++) { if (condition()) return; await Task.Delay(10); }
        throw new InvalidOperationException(error);
    }
    private async Task RefreshAsync()
    {
        if (!ready || closing || !await operations.WaitAsync(0)) return;
        try { await ReadStateAsync(); }
        catch (Exception ex) { timer.Stop(); ready = false; ShowError(ex); }
        finally { operations.Release(); }
    }
    private async Task ReadStateAsync()
    {
        snapshot = await engine.Client.GetDownloadsAsync(lifetime.Token);
        var stats = await engine.Client.RequestAsync(new(0x0a, EcTag.Integer(4, 0)), lifetime.Token);
        QueueCount.Text = snapshot.Count.ToString();
        DownloadSpeed.Text = DownloadItem.FormatBytes(stats.Find(0x201)?.Number ?? 0) + "/s";
        UploadSpeed.Text = DownloadItem.FormatBytes(stats.Find(0x200)?.Number ?? 0) + "/s";
        var network = NetworkState.FromTag(stats.Find(5) ?? throw new InvalidDataException("Falta estado de red."));
        ConnectionStatus.Text = $"Motor local conectado   |   eD2k: {network.Ed2kText}   |   Kad: {network.KadText}";
        ApplyFilter();
    }
    private void ApplyFilter()
    {
        if (FilterInput == null) return;
        string? selected = (DownloadsGrid.SelectedItem as DownloadItem)?.Hash;
        var filtered = snapshot.Where(d => d.Name.Contains(FilterInput.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        var valid = filtered.Select(d => d.Hash).ToHashSet();
        for (int i = rows.Count - 1; i >= 0; i--) if (!valid.Contains(rows[i].Hash)) rows.RemoveAt(i);
        foreach (var item in filtered)
        {
            int index = -1; for (int i = 0; i < rows.Count; i++) if (rows[i].Hash == item.Hash) { index = i; break; }
            if (index < 0) rows.Add(item); else if (rows[index] != item) rows[index] = item;
        }
        DownloadsGrid.SelectedItem = rows.FirstOrDefault(d => d.Hash == selected);
        EmptyState.IsVisible = rows.Count == 0;
        EmptyTitle.Text = snapshot.Count == 0 ? "Tu próxima descarga empieza aquí" : "No hay resultados para este filtro";
    }
    private void FilterChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();
    private void SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PauseButton == null) return;
        PauseButton.IsEnabled = ResumeButton.IsEnabled = ready && !closing && DownloadsGrid.SelectedItem is DownloadItem;
    }
    private async Task ActAsync(Func<Task> action, string success)
    {
        if (!ready || closing) return;
        await operations.WaitAsync();
        try { await action(); Message.Text = success; await ReadStateAsync(); }
        catch (ArgumentException ex) { Message.Text = ex.Message; }
        catch (EcCommandException ex) { Message.Text = ex.Message; }
        catch (Exception ex) { ready = false; timer.Stop(); ShowError(ex); }
        finally { operations.Release(); }
    }
    private async void AddLink(object? sender, RoutedEventArgs e)
    {
        string link = LinkInput.Text?.Trim() ?? "";
        await ActAsync(async () => { await engine.Client.AddLinkAsync(link, lifetime.Token); LinkInput.Text = ""; }, "Enlace añadido. Puedes gestionar la conexión desde Servidores.");
    }
    private void LinkKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddLink(sender, new RoutedEventArgs()); }
    private async void PauseSelected(object? sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is DownloadItem item) await ActAsync(async () => { await engine.Client.PauseAsync(item.Hash, true, lifetime.Token); }, "Descarga pausada.");
    }
    private async void ResumeSelected(object? sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is DownloadItem item) await ActAsync(async () => { await engine.Client.PauseAsync(item.Hash, false, lifetime.Token); }, "Descarga reanudada en la cola.");
    }
    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await RefreshAsync();
    private async void OpenServers(object? sender, RoutedEventArgs e)
    {
        if (!ready || closing) return;
        await new ServersWindow(engine.Client).ShowDialog(this);
        await RefreshAsync();
    }
    private void OpenDownloads(object? sender, RoutedEventArgs e) => OpenPath(Path.Combine(engine.ProfilePath, "Incoming"));
    private void OpenPlan(object? sender, RoutedEventArgs e) => OpenPath(Path.Combine(repository, "docs", "PLAN.md"));
    private void OpenPath(string path)
    {
        try { if (File.Exists(path) || Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Message.Text = ex.Message; }
    }
    private void ShowError(Exception ex)
    {
        EngineBadge.Text = "●  Requiere atención";
        Message.Text = ex.Message;
        ConnectionStatus.Text = "Sin conexión EC verificada. Cierra y vuelve a abrir para reintentar.";
        AddButton.IsEnabled = PauseButton.IsEnabled = ResumeButton.IsEnabled = RefreshButton.IsEnabled = ServersButton.IsEnabled = false;
    }
    private async void WindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; timer.Stop();
        Message.Text = "Guardando la cola y deteniendo el motor…";
        await operations.WaitAsync();
        try
        {
            await engine.StopAsync(); lifetime.Cancel();
            allowClose = true; Close();
        }
        catch (Exception ex) { closing = false; Message.Text = "No se pudo cerrar el motor: " + ex.Message + " Puedes volver a intentar cerrar."; }
        finally { operations.Release(); }
    }
}
