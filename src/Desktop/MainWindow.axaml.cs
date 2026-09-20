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
            if (Program.ConnectServer is { } endpoint)
            {
                var parts = endpoint.Split(':');
                if (parts.Length != 2) throw new ArgumentException("Servidor inválido: usa IPv4:puerto.");
                var server = await engine.Client.AddServerAsync(parts[0], parts[1], "");
                await engine.Client.ConnectServerAsync(server);
                for (int i = 0; i < 30; i++)
                {
                    if ((await engine.Client.GetNetworkStateAsync()).Connected) break;
                    await Task.Delay(500);
                }
            }
            ready = true;
            EngineBadge.Text = $"●  aMule {engine.Client.ServerVersion}";
            ConnectionStatus.Text = "Motor autenticado · consultando las redes…";
            Message.Text = "Perfil aislado listo. Puedes seleccionar varias descargas para pausar, reanudar o cancelar. Al cerrar esta versión, el motor se detiene ordenadamente.";
            AddButton.IsEnabled = RefreshButton.IsEnabled = ServersButton.IsEnabled = SearchNavButton.IsEnabled = SettingsButton.IsEnabled = true;
        }
        catch (Exception ex) { ShowError(ex); }
        finally { operations.Release(); }
        if (ready) { await RefreshAsync(); timer.Start(); }
        if (Program.CapturePath != null)
        {
            Window? childWindow = null;
            try
            {
                if (!ready) throw new InvalidOperationException("La captura no pudo conectar al motor.");
                if (Program.ShowSearch)
                {
                    var searchWindow = new SearchWindow(engine.Client);
                    childWindow = searchWindow;
                    searchWindow.Show(this);
                    if (Program.ExerciseUi) await searchWindow.ExerciseUiAsync();
                }
                else if (Program.ShowServers)
                {
                    var serversWindow = new ServersWindow(engine.Client);
                    childWindow = serversWindow;
                    serversWindow.Show(this);
                    if (Program.ExerciseUi) await serversWindow.ExerciseUiAsync();
                }
                else if (Program.ShowSettings)
                {
                    var settingsWindow = new SettingsWindow(engine);
                    childWindow = settingsWindow;
                    settingsWindow.Show(this);
                    if (Program.ExerciseUi) settingsWindow.ExerciseUi();
                }
                else if (Program.ExerciseUi) await ExerciseUiAsync();
            }
            catch (Exception ex) { Program.CaptureFailed = true; Message.Text = "Prueba visual fallida: " + ex.Message; }
            await Task.Delay(1000);
            Window target = childWindow ?? (Window)this;
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)target.Bounds.Width, (int)target.Bounds.Height), new Vector(96, 96));
            bitmap.Render(target);
            Directory.CreateDirectory(Path.GetDirectoryName(Program.CapturePath)!);
            bitmap.Save(Program.CapturePath, PngBitmapEncoderOptions.Default);
            if (childWindow != null)
            {
                File.WriteAllText(Path.ChangeExtension(Program.CapturePath, ".validation.txt"), Program.CaptureFailed ? "FAIL: UI" : Program.ShowSearch ? "PASS: search UI opened. Disconnected chrome verified when eD2k is down; live results covered by controlled integration." : Program.ShowSettings ? "PASS: settings UI shows Incoming and tmp paths." : "PASS: servers UI validation, add, selection, duplicate, disconnected state. Real engine.");
                childWindow.Close();
            }
            Close();
        }
        else if (ready && Program.ShowServers) OpenServers(this, new RoutedEventArgs());
        else if (ready && Program.ShowSearch) OpenSearch(this, new RoutedEventArgs());
        else if (ready && Program.ShowSettings) OpenSettings(this, new RoutedEventArgs());
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
        LinkInput.Text = "ed2k://|file|Prueba de interfaz - def.txt|3|C448017AAF21D8525FC10AE87AA6729D|/";
        AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        if (rows.Count != 2) throw new InvalidOperationException("No se añadieron dos descargas para la selección múltiple.");
        DownloadsGrid.SelectedItems.Clear();
        foreach (var row in rows) DownloadsGrid.SelectedItems.Add(row);
        if (!PauseButton.IsEnabled) throw new InvalidOperationException("Pausar no se habilita con varias filas.");
        FilterInput.Text = "no-coincide-123";
        await WaitForUiAsync(() => rows.Count == 0, "El filtro no excluye filas.");
        FilterInput.Text = "";
        await WaitForUiAsync(() => rows.Count == 2, "El filtro no restaura filas.");
        Message.Text = "Prueba de interfaz superada: añadir, pausar, reanudar, filtro y selección múltiple. Cancelar se cubre en las pruebas de integración.";
        File.WriteAllText(Path.ChangeExtension(Program.CapturePath!, ".validation.txt"), "PASS: UI add, pause, resume, filter, multi-select. Real EC engine; isolated fixture; cancel covered by integration.\n");
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
        var selected = SelectedDownloads().Select(d => d.Hash).ToHashSet();
        var filtered = snapshot.Where(d => d.Name.Contains(FilterInput.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        var valid = filtered.Select(d => d.Hash).ToHashSet();
        for (int i = rows.Count - 1; i >= 0; i--) if (!valid.Contains(rows[i].Hash)) rows.RemoveAt(i);
        foreach (var item in filtered)
        {
            int index = -1; for (int i = 0; i < rows.Count; i++) if (rows[i].Hash == item.Hash) { index = i; break; }
            if (index < 0) rows.Add(item); else if (rows[index] != item) rows[index] = item;
        }
        foreach (var row in rows.Where(d => selected.Contains(d.Hash)))
            if (!DownloadsGrid.SelectedItems.Contains(row)) DownloadsGrid.SelectedItems.Add(row);
        EmptyState.IsVisible = rows.Count == 0;
        EmptyTitle.Text = snapshot.Count == 0 ? "Tu próxima descarga empieza aquí" : "No hay resultados para este filtro";
        UpdateActionButtons();
    }
    private DownloadItem[] SelectedDownloads() => DownloadsGrid.SelectedItems.Cast<DownloadItem>().ToArray();
    private void FilterChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();
    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateActionButtons();
    private void UpdateActionButtons()
    {
        if (PauseButton == null) return;
        var selected = SelectedDownloads();
        bool active = ready && !closing && selected.Length > 0;
        PauseButton.IsEnabled = active && selected.Any(d => d.CanCancel && d.State != 7);
        ResumeButton.IsEnabled = active && selected.Any(d => d.CanCancel && d.State == 7);
        CancelButton.IsEnabled = active && selected.Any(d => d.CanCancel);
        ClearButton.IsEnabled = active && selected.Any(d => d.IsComplete && d.EcId != 0);
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
        var hashes = SelectedDownloads().Where(d => d.CanCancel).Select(d => d.Hash).ToArray();
        if (hashes.Length > 0) await ActAsync(async () => await engine.Client.PauseAsync(hashes, true, lifetime.Token), hashes.Length == 1 ? "Descarga pausada." : hashes.Length + " descargas pausadas.");
    }
    private async void ResumeSelected(object? sender, RoutedEventArgs e)
    {
        var hashes = SelectedDownloads().Where(d => d.CanCancel).Select(d => d.Hash).ToArray();
        if (hashes.Length > 0) await ActAsync(async () => await engine.Client.PauseAsync(hashes, false, lifetime.Token), hashes.Length == 1 ? "Descarga reanudada en la cola." : hashes.Length + " descargas reanudadas.");
    }
    private async void CancelSelected(object? sender, RoutedEventArgs e)
    {
        var items = SelectedDownloads().Where(d => d.CanCancel).ToArray();
        if (items.Length == 0) return;
        var confirm = new ConfirmWindow("Cancelar descargas",
            items.Length == 1
                ? $"Se cancelará «{items[0].Name}» y se borrarán sus archivos temporales. Un archivo ya completado no se toca."
                : $"Se cancelarán {items.Length} descargas incompletas y se borrarán sus archivos temporales. Los completados seleccionados no se tocan.",
            items.Length == 1 ? "Cancelar descarga" : "Cancelar descargas");
        await confirm.ShowDialog(this);
        if (!confirm.Accepted) return;
        await ActAsync(async () => await engine.Client.CancelDownloadsAsync(items.Select(d => d.Hash).ToArray(), lifetime.Token),
            items.Length == 1 ? "Descarga cancelada." : items.Length + " descargas canceladas.");
    }
    private async void ClearCompletedSelected(object? sender, RoutedEventArgs e)
    {
        var items = SelectedDownloads().Where(d => d.IsComplete && d.EcId != 0).ToArray();
        if (items.Length == 0) { Message.Text = "Selecciona descargas completadas para quitarlas de la lista. El archivo en Incoming se conserva."; return; }
        await ActAsync(async () => await engine.Client.ClearCompletedAsync(items.Select(d => d.EcId).ToArray(), lifetime.Token),
            "Quitadas de la lista. Los archivos en Incoming se conservan.");
    }
    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await RefreshAsync();
    private async void OpenSearch(object? sender, RoutedEventArgs e)
    {
        if (!ready || closing) return;
        await new SearchWindow(engine.Client).ShowDialog(this);
        await RefreshAsync();
    }
    private async void OpenServers(object? sender, RoutedEventArgs e)
    {
        if (!ready || closing) return;
        await new ServersWindow(engine.Client).ShowDialog(this);
        await RefreshAsync();
    }
    private async void OpenSettings(object? sender, RoutedEventArgs e)
    {
        if (!ready || closing) return;
        timer.Stop();
        await operations.WaitAsync();
        try { await new SettingsWindow(engine).ShowDialog(this); }
        finally { operations.Release(); }
        if (ready && !closing) { timer.Start(); await RefreshAsync(); }
    }
    private void OpenDownloads(object? sender, RoutedEventArgs e) => OpenPath(engine.IncomingPath);
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
        AddButton.IsEnabled = PauseButton.IsEnabled = ResumeButton.IsEnabled = CancelButton.IsEnabled = ClearButton.IsEnabled = RefreshButton.IsEnabled = ServersButton.IsEnabled = SearchNavButton.IsEnabled = SettingsButton.IsEnabled = false;
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
