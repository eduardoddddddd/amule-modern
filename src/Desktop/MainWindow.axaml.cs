using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using AmuleModern.Amule;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AmuleModern.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DownloadItem> rows = [];
    private IReadOnlyList<DownloadItem> snapshot = [];
    private readonly Dictionary<string, double> averageSpeed = new(StringComparer.Ordinal);
    private readonly EngineSession engine = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool ready, allowClose, quitting, startupAttempted, themeReady, densityReady;
    private string repository = "";
    private string? detailFolder;
    private string? detailLink;
    private string currentPage = "downloads";
    private TrayIcon? tray;
    private SearchWindow? searchPage;
    private ServersWindow? serversPage;
    private SharedWindow? sharedPage;
    private SettingsWindow? settingsPage;
    private LogWindow? logPage;

    public MainWindow()
    {
        InitializeComponent();
        DownloadsGrid.ItemsSource = rows;
        SelectThemeItem(UiSettings.LoadTheme());
        SelectDensityItem(UiSettings.LoadDensity());
        themeReady = densityReady = true;
        GridColumns.Attach(DownloadsGrid, "downloads", ["name", "size", "progress", "state", "speed", "eta", "sources"], ColumnsButton);
        DownloadsGrid.AddHandler(PointerPressedEvent, SelectRowUnderPointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        Opened += WindowOpened;
        Closing += WindowClosing;
        timer.Tick += async (_, _) => await RefreshAsync();
        if (Program.CapturePath == null) AttachTray();
    }

    private void SelectThemeItem(string theme)
    {
        for (int i = 0; i < ThemeInput.Items.Count; i++)
        {
            if (ThemeInput.Items[i] is ComboBoxItem item && (item.Tag as string) == theme)
            {
                ThemeInput.SelectedIndex = i;
                return;
            }
        }
        ThemeInput.SelectedIndex = 0;
    }

    private void SelectDensityItem(string density)
    {
        for (int i = 0; i < DensityInput.Items.Count; i++)
        {
            if (DensityInput.Items[i] is ComboBoxItem item && (item.Tag as string) == density)
            {
                DensityInput.SelectedIndex = i;
                return;
            }
        }
        DensityInput.SelectedIndex = 0;
    }

    private void DensityChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!densityReady || DensityInput.SelectedItem is not ComboBoxItem item || item.Tag is not string density) return;
        GridColumns.SetDensity(density);
    }

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!themeReady || ThemeInput.SelectedItem is not ComboBoxItem item || item.Tag is not string theme) return;
        App.ApplyTheme(theme);
    }

    private bool CanLeaveCurrentPage()
    {
        if (settingsPage != null && currentPage == "settings" && settingsPage.IsBusy)
        {
            Message.Text = "Espera a que terminen los ajustes antes de cambiar de pantalla.";
            return false;
        }
        return true;
    }

    private void SetNavSelected(Button selected)
    {
        foreach (var btn in new[] { NavDownloads, NavSearch, NavServers, NavShared, NavSettings, NavLog })
        {
            btn.Classes.Remove("selected");
            if (btn == selected) btn.Classes.Add("selected");
        }
    }

    private void ShowDownloadsPage()
    {
        currentPage = "downloads";
        PageHost.IsVisible = false;
        PageHost.Content = null;
        DownloadsPanel.IsVisible = true;
        SetNavSelected(NavDownloads);
        if (ready && !quitting && !timer.IsEnabled) timer.Start();
    }

    private void ShowPage(string id, Control page, Button nav)
    {
        if (!CanLeaveCurrentPage()) return;
        currentPage = id;
        DownloadsPanel.IsVisible = false;
        PageHost.Content = page;
        PageHost.IsVisible = true;
        SetNavSelected(nav);
    }

    private void NavDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        if (!CanLeaveCurrentPage()) return;
        ShowDownloadsPage();
        _ = RefreshAsync();
    }

    private void NavSearchClicked(object? sender, RoutedEventArgs e)
    {
        if (!ready || quitting) return;
        searchPage ??= new SearchWindow(() => engine.Client);
        searchPage.GoToDownloads -= OnGoToDownloads;
        searchPage.GoToDownloads += OnGoToDownloads;
        ShowPage("search", searchPage, NavSearch);
    }

    private void OnGoToDownloads() => ShowDownloadsPage();

    private void NavServersClicked(object? sender, RoutedEventArgs e)
    {
        if (!ready || quitting) return;
        serversPage ??= new ServersWindow(() => engine.Client);
        ShowPage("servers", serversPage, NavServers);
    }

    private void NavSharedClicked(object? sender, RoutedEventArgs e)
    {
        if (!ready || quitting) return;
        sharedPage ??= new SharedWindow(engine);
        ShowPage("shared", sharedPage, NavShared);
    }

    private void NavSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (!ready || quitting) return;
        settingsPage ??= new SettingsWindow(engine);
        ShowPage("settings", settingsPage, NavSettings);
    }

    private void NavLogClicked(object? sender, RoutedEventArgs e)
    {
        if (!ready || quitting) return;
        logPage ??= new LogWindow(() => engine.Client);
        ShowPage("log", logPage, NavLog);
    }

    private async void WindowOpened(object? sender, EventArgs e)
    {
        if (startupAttempted) return;
        startupAttempted = true;
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
            Message.Text = Program.CapturePath == null
                ? "Perfil listo. La X oculta a la bandeja y las transferencias siguen. «Salir y detener» cierra el motor."
                : "Perfil de captura listo. Al cerrar, el motor se detiene.";
            AddButton.IsEnabled = RefreshButton.IsEnabled = NavServers.IsEnabled = NavSearch.IsEnabled =
                NavSettings.IsEnabled = NavShared.IsEnabled = NavLog.IsEnabled = true;
            if (Program.CapturePath == null)
            {
                SingleInstance.Watch(link => Dispatcher.UIThread.Post(() => _ = ShowFromTrayAsync(link)), lifetime.Token);
                if (!string.IsNullOrWhiteSpace(Program.StartupLink))
                    await engine.Client.AddLinkAsync(Program.StartupLink, lifetime.Token);
            }
        }
        catch (Exception ex) { ShowError(ex); }
        finally { operations.Release(); }
        if (ready) { await RefreshAsync(); timer.Start(); }
        if (Program.CapturePath != null)
        {
            try
            {
                if (!ready) throw new InvalidOperationException("La captura no pudo conectar al motor.");
                if (Program.ShowSearch)
                {
                    NavSearchClicked(this, new RoutedEventArgs());
                    if (Program.ExerciseUi) await searchPage!.ExerciseUiAsync();
                }
                else if (Program.ShowServers)
                {
                    NavServersClicked(this, new RoutedEventArgs());
                    if (Program.ExerciseUi) await serversPage!.ExerciseUiAsync();
                }
                else if (Program.ShowSettings)
                {
                    NavSettingsClicked(this, new RoutedEventArgs());
                    if (Program.ExerciseUi) settingsPage!.ExerciseUi();
                }
                else if (Program.ShowShared)
                {
                    NavSharedClicked(this, new RoutedEventArgs());
                    if (Program.ExerciseUi) await sharedPage!.ExerciseUiAsync();
                }
                else if (Program.ExerciseUi) await ExerciseUiAsync();
            }
            catch (Exception ex) { Program.CaptureFailed = true; Message.Text = "Prueba visual fallida: " + ex.Message; }
            await Task.Delay(1000);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96));
            bitmap.Render(this);
            Directory.CreateDirectory(Path.GetDirectoryName(Program.CapturePath)!);
            bitmap.Save(Program.CapturePath, PngBitmapEncoderOptions.Default);
            if (Program.ShowSearch || Program.ShowServers || Program.ShowSettings || Program.ShowShared)
            {
                File.WriteAllText(Path.ChangeExtension(Program.CapturePath, ".validation.txt"),
                    Program.CaptureFailed ? "FAIL: UI" : !Program.ExerciseUi ? "CAPTURE ONLY: UI not exercised."
                    : Program.ShowSearch ? "PASS: search UI; simulated Kad-only active search retains Stop, survives scope change, stops on network loss. Real search covered separately by integration."
                    : Program.ShowSettings ? "PASS: settings UI shows Incoming, tmp, bandwidth limits and Kad controls."
                    : Program.ShowShared ? "PASS: shared UI removes empty folder through picker and confirmation; directory retained. Incoming and filter verified."
                    : "PASS: servers UI validation, add, import, remove, duplicate, disconnected state. Real engine.");
            }
            Close();
        }
        else if (ready && Program.ShowServers) NavServersClicked(this, new RoutedEventArgs());
        else if (ready && Program.ShowSearch) NavSearchClicked(this, new RoutedEventArgs());
        else if (ready && Program.ShowSettings) NavSettingsClicked(this, new RoutedEventArgs());
        else if (ready && Program.ShowShared) NavSharedClicked(this, new RoutedEventArgs());
    }

    private async Task ExerciseUiAsync()
    {
        int? runningMotor = engine.ProcessId;
        Hide(); Show();
        await operations.WaitAsync(); operations.Release();
        if (engine.ProcessId != runningMotor || !AddButton.IsEnabled || !NavSearch.IsEnabled)
            throw new InvalidOperationException("Restaurar la ventana intentó reiniciar el motor o deshabilitó controles.");
        LinkInput.Text = "ed2k://|file|Prueba de interfaz - abc.txt|3|A448017AAF21D8525FC10AE87AA6729D|/";
        AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        var first = rows.SingleOrDefault(r => r.Hash == "A448017AAF21D8525FC10AE87AA6729D") ?? throw new InvalidOperationException("El botón Añadir no actualizó la tabla.");
        DownloadsGrid.SelectedItem = first;
        string hashLine = DetailHash.Text ?? "";
        string linkLine = DetailLink.Text ?? "";
        string pathLine = DetailPath.Text ?? "";
        string factsLine = DetailFacts.Text ?? "";
        if (!hashLine.Contains(first.Hash, StringComparison.Ordinal)
            || !linkLine.Contains(first.Hash, StringComparison.Ordinal)
            || !pathLine.EndsWith(".part", StringComparison.Ordinal)
            || factsLine.Length == 0
            || !factsLine.Contains("Transfiriendo", StringComparison.Ordinal))
            throw new InvalidOperationException("El panel de detalle no muestra hash, enlace, parcial o fuentes.");
        var menuCheck = new CancelEventArgs();
        DownloadsMenuOpening(null, menuCheck);
        if (menuCheck.Cancel || !MenuPause.IsEnabled || MenuResume.IsEnabled || !MenuCopyHash.IsEnabled || !MenuCopyLink.IsEnabled)
            throw new InvalidOperationException("El menú contextual no refleja la descarga seleccionada.");
        PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        if (rows[0].State != 7) throw new InvalidOperationException("El botón Pausar no pausó la cola real.");
        if (rows[0].EtaText != "—") throw new InvalidOperationException("Una descarga pausada no debe mostrar tiempo restante.");
        DownloadsGrid.SelectedItem = rows[0];
        menuCheck = new CancelEventArgs();
        DownloadsMenuOpening(null, menuCheck);
        if (!MenuResume.IsEnabled || MenuPause.IsEnabled) throw new InvalidOperationException("El menú contextual no ofrece Reanudar tras pausar.");
        if (DownloadItem.FormatEta(90) != "1 min" || DownloadItem.FormatEta(3 * 3600 + 5 * 60) != "3 h 05 min" || DownloadItem.FormatEta(40 * 86400) != "> 30 d")
            throw new InvalidOperationException("Formato de tiempo restante incorrecto.");
        ResumeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        if (rows[0].State == 7) throw new InvalidOperationException("El botón Reanudar no reanudó la cola real.");
        PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        LinkInput.Text = "ed2k://|file|Prueba de interfaz - def.txt|3|C448017AAF21D8525FC10AE87AA6729D|/";
        AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await operations.WaitAsync(); operations.Release();
        if (!rows.Any(r => r.Hash == "C448017AAF21D8525FC10AE87AA6729D")) throw new InvalidOperationException("No se añadieron dos descargas para la selección múltiple.");
        int unfilteredCount = rows.Count;
        DownloadsGrid.SelectedItems.Clear();
        foreach (var row in rows) DownloadsGrid.SelectedItems.Add(row);
        if (rows.Any(r => r.CanCancel && r.State == 7) && ResumeButton.IsEnabled)
        {
            ResumeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await operations.WaitAsync(); operations.Release();
            DownloadsGrid.SelectedItems.Clear();
            foreach (var row in rows) DownloadsGrid.SelectedItems.Add(row);
        }
        if (!PauseButton.IsEnabled) throw new InvalidOperationException("Pausar no se habilita con varias filas.");
        FilterInput.Text = "no-coincide-123";
        await WaitForUiAsync(() => rows.Count == 0, "El filtro no excluye filas.");
        FilterInput.Text = "";
        await WaitForUiAsync(() => rows.Count == unfilteredCount, "El filtro no restaura filas.");
        Message.Text = "Prueba de interfaz superada: añadir, pausar, reanudar, filtro y selección múltiple. Cancelar se cubre en las pruebas de integración.";
        File.WriteAllText(Path.ChangeExtension(Program.CapturePath!, ".validation.txt"), "PASS: Hide/Show preserves motor and controls; UI add, pause, resume, filter, multi-select, context menu state, ETA. Real EC engine; isolated fixture; cancel covered by integration.\n");
    }

    private static async Task WaitForUiAsync(Func<bool> condition, string error)
    {
        for (int i = 0; i < 100; i++) { if (condition()) return; await Task.Delay(10); }
        throw new InvalidOperationException(error);
    }

    // Settings may stop and restart the engine; polling must not treat that as a lost connection.
    private bool EngineBusy => settingsPage?.IsBusy == true;

    private async Task RefreshAsync()
    {
        if (!ready || quitting || EngineBusy || !await operations.WaitAsync(0)) return;
        try { await ReadStateAsync(); }
        catch (Exception) when (EngineBusy) { }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            try { await engine.ReconnectAsync(lifetime.Token); await ReadStateAsync(); Message.Text = "Conexión EC restablecida. El motor no se ha reiniciado."; }
            catch (Exception reconnect) { timer.Stop(); ready = false; ShowError(reconnect); }
        }
        catch (Exception ex) { timer.Stop(); ready = false; ShowError(ex); }
        finally { operations.Release(); }
    }

    private async Task ReadStateAsync()
    {
        bool downloads = currentPage == "downloads" && IsVisible;
        if (downloads)
        {
            snapshot = WithAverageSpeed(await engine.Client.GetDownloadsAsync(lifetime.Token));
            QueueCount.Text = snapshot.Count.ToString();
        }
        var stats = await engine.Client.RequestAsync(new(0x0a, EcTag.Integer(4, 0)), lifetime.Token);
        string down = DownloadItem.FormatBytes(stats.Find(0x201)?.Number ?? 0) + "/s";
        string up = DownloadItem.FormatBytes(stats.Find(0x200)?.Number ?? 0) + "/s";
        DownloadSpeed.Text = down;
        UploadSpeed.Text = up;
        var network = NetworkState.FromTag(stats.Find(5) ?? throw new InvalidDataException("Falta estado de red."));
        EngineBadge.Text = "●  " + network.Ed2kText;
        ConnectionStatus.Text = $"eD2k: {network.Ed2kText}   |   Kad: {network.KadText}   |   ↓ {down}   ↑ {up}";
        if (tray != null) tray.ToolTipText = $"aMule Modern · ↓ {down}  ↑ {up}";
        if (downloads) ApplyFilter();
    }

    // Exponential average over roughly the last ten polls, so the ETA does not jump with each sample.
    private IReadOnlyList<DownloadItem> WithAverageSpeed(IReadOnlyList<DownloadItem> items)
    {
        var result = new DownloadItem[items.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            seen.Add(item.Hash);
            double average = item.IsComplete || item.State == 7 ? 0
                : averageSpeed.TryGetValue(item.Hash, out double previous) ? previous * 0.8 + item.Speed * 0.2
                : item.Speed;
            averageSpeed[item.Hash] = average;
            result[i] = item with { AverageSpeed = average };
        }
        foreach (string gone in averageSpeed.Keys.Where(hash => !seen.Contains(hash)).ToArray()) averageSpeed.Remove(gone);
        return result;
    }

    private void SelectRowUnderPointer(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(DownloadsGrid).Properties.IsRightButtonPressed) return;
        var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        if (row?.DataContext is DownloadItem item && !DownloadsGrid.SelectedItems.Contains(item))
            DownloadsGrid.SelectedItem = item;
    }

    private void DownloadsMenuOpening(object? sender, CancelEventArgs e)
    {
        UpdateActionButtons();
        if (SelectedDownloads().Length == 0) { e.Cancel = true; return; }
        MenuPause.IsEnabled = PauseButton.IsEnabled;
        MenuResume.IsEnabled = ResumeButton.IsEnabled;
        MenuCancel.IsEnabled = CancelButton.IsEnabled;
        MenuClear.IsEnabled = ClearButton.IsEnabled;
        MenuCopyHash.IsEnabled = CopyHashButton.IsEnabled;
        MenuCopyLink.IsEnabled = CopyLinkButton.IsEnabled;
        MenuOpenFolder.IsEnabled = OpenFolderButton.IsEnabled;
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
        var keep = rows.Where(d => selected.Contains(d.Hash)).ToArray();
        var now = SelectedDownloads();
        if (now.Length != keep.Length || now.Any(d => !selected.Contains(d.Hash)))
        {
            DownloadsGrid.SelectedItems.Clear();
            foreach (var row in keep) DownloadsGrid.SelectedItems.Add(row);
        }
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
        bool active = ready && !quitting && selected.Length > 0;
        PauseButton.IsEnabled = active && selected.Any(d => d.CanCancel && d.State != 7);
        ResumeButton.IsEnabled = active && selected.Any(d => d.CanCancel && d.State == 7);
        CancelButton.IsEnabled = active && selected.Any(d => d.CanCancel);
        ClearButton.IsEnabled = active && selected.Any(d => d.IsComplete && d.EcId != 0);
        ShowDetail(DownloadsGrid.SelectedItem as DownloadItem);
    }

    private void ShowDetail(DownloadItem? item)
    {
        detailFolder = null;
        detailLink = null;
        bool show = item != null;
        DetailHint.IsVisible = !show;
        DetailHash.IsVisible = DetailPath.IsVisible = DetailMeta.IsVisible = DetailLink.IsVisible = DetailFacts.IsVisible = show;
        CopyHashButton.IsEnabled = CopyLinkButton.IsEnabled = OpenFolderButton.IsEnabled = false;
        if (item == null) return;
        var place = DownloadLocation.Resolve(engine.IncomingPath, engine.TempPath, item);
        detailFolder = place.Folder;
        detailLink = item.Detail.Link is { Length: > 0 } engineLink && engineLink.StartsWith("ed2k://", StringComparison.OrdinalIgnoreCase)
            ? engineLink
            : item.Name.Contains('|') ? null : $"ed2k://|file|{item.Name}|{item.Size}|{item.Hash}|/";
        DetailHash.Text = "Hash  " + item.Hash;
        DetailPath.Text = place.DataFile ?? "Ruta no disponible";
        DetailMeta.Text = item.IsComplete ? "Archivo en Incoming" : place.MetaFile is { } met ? "Metadatos  " + met : "Metadatos no disponibles";
        DetailLink.Text = detailLink ?? "Enlace no disponible";
        string comment = item.Detail.Comment switch { null => "No disponible", "" => "Sin comentario", var text => text };
        DetailFacts.Text = item.Detail.PriorityText
            + "  ·  " + item.Detail.SourcesText
            + "  ·  visto completo " + DownloadDetail.UnixText(item.Detail.LastSeenUnix)
            + "  ·  última recepción " + DownloadDetail.UnixText(item.Detail.LastRecvUnix)
            + "  ·  activo " + DownloadDetail.DurationText(item.Detail.ActiveSeconds)
            + "  ·  partes " + DownloadDetail.Num(item.Detail.AvailableParts)
            + "  ·  AICH " + (item.Detail.Aich ?? "No disponible")
            + "  ·  comentario " + comment;
        CopyHashButton.IsEnabled = true;
        CopyLinkButton.IsEnabled = detailLink != null;
        OpenFolderButton.IsEnabled = detailFolder != null && Directory.Exists(detailFolder);
    }

    private async void CopyHash(object? sender, RoutedEventArgs e)
    {
        if (DownloadsGrid.SelectedItem is not DownloadItem item) return;
        await CopyTextAsync(item.Hash, "Hash copiado.");
    }

    private async void CopyLink(object? sender, RoutedEventArgs e)
    {
        if (detailLink == null) return;
        await CopyTextAsync(detailLink, "Enlace copiado.");
    }

    private async Task CopyTextAsync(string text, string done)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) { Message.Text = "No hay portapapeles en esta sesión."; return; }
        await clipboard.SetTextAsync(text);
        Message.Text = done;
    }

    private void OpenDetailFolder(object? sender, RoutedEventArgs e)
    {
        if (detailFolder != null) OpenPath(detailFolder);
    }

    private async Task ActAsync(Func<Task> action, string success)
    {
        if (!ready || quitting) return;
        await operations.WaitAsync();
        try { await action(); Message.Text = success; await ReadStateAsync(); }
        catch (ArgumentException ex) { Message.Text = ex.Message; }
        catch (EcCommandException ex) { Message.Text = ex.Message; }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            try { await engine.ReconnectAsync(lifetime.Token); await action(); Message.Text = success + " Conexión EC restablecida."; await ReadStateAsync(); }
            catch (Exception reconnect) { ready = false; timer.Stop(); ShowError(reconnect); }
        }
        catch (Exception ex) { ready = false; timer.Stop(); ShowError(ex); }
        finally { operations.Release(); }
    }

    private static bool IsRecoverable(Exception ex) =>
        ex is SocketException or IOException or ObjectDisposedException or EndOfStreamException or TimeoutException or InvalidDataException
        || ex.InnerException is SocketException or IOException or ObjectDisposedException;

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
        await ActAsync(async () => { await engine.Client.CancelDownloadsAsync(items.Select(d => d.Hash).ToArray(), lifetime.Token); await RememberQueueAsync(); },
            items.Length == 1 ? "Descarga cancelada." : items.Length + " descargas canceladas.");
    }

    private async void ClearCompletedSelected(object? sender, RoutedEventArgs e)
    {
        var items = SelectedDownloads().Where(d => d.IsComplete && d.EcId != 0).ToArray();
        if (items.Length == 0) { Message.Text = "Selecciona descargas completadas para quitarlas de la lista. El archivo en Incoming se conserva."; return; }
        await ActAsync(async () => { await engine.Client.ClearCompletedAsync(items.Select(d => d.EcId).ToArray(), lifetime.Token); await RememberQueueAsync(); },
            "Quitadas de la lista. Los archivos en Incoming se conservan.");
    }

    // Without this, a crash before the next periodic snapshot would restore what was just cancelled.
    private async Task RememberQueueAsync()
    {
        try { await engine.RememberSnapshotsAsync(lifetime.Token); }
        catch (Exception) { /* la copia periódica lo reintenta */ }
    }

    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await RefreshAsync();
    private void OpenDownloads(object? sender, RoutedEventArgs e) => OpenPath(engine.IncomingPath);

    private void OpenPath(string path)
    {
        try { if (File.Exists(path) || Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Message.Text = ex.Message; }
    }

    private void ShowError(Exception ex)
    {
        EngineBadge.Text = "●  Requiere atención";
        Message.Text = ex.Message;
        ConnectionStatus.Text = "Sin conexión EC verificada. Si el motor sigue, recarga; «Salir y detener» cierra el proceso.";
        AddButton.IsEnabled = PauseButton.IsEnabled = ResumeButton.IsEnabled = CancelButton.IsEnabled = ClearButton.IsEnabled =
            RefreshButton.IsEnabled = NavServers.IsEnabled = NavSearch.IsEnabled = NavSettings.IsEnabled = NavShared.IsEnabled = NavLog.IsEnabled =
            CopyHashButton.IsEnabled = CopyLinkButton.IsEnabled = OpenFolderButton.IsEnabled = false;
    }

    private void AttachTray()
    {
        var show = new NativeMenuItem("Mostrar ventana");
        show.Click += (_, _) => Dispatcher.UIThread.Post(() => _ = ShowFromTrayAsync(null));
        var quit = new NativeMenuItem("Salir y detener");
        quit.Click += (_, _) => Dispatcher.UIThread.Post(() => _ = QuitAsync());
        tray = new TrayIcon
        {
            Icon = TrayGlyph.Create(),
            ToolTipText = "aMule Modern",
            Menu = new NativeMenu { Items = { show, quit } }
        };
        tray.Clicked += (_, _) => Dispatcher.UIThread.Post(() => _ = ShowFromTrayAsync(null));
        if (Application.Current != null) TrayIcon.SetIcons(Application.Current, [tray]);
    }

    private async Task ShowFromTrayAsync(string? link)
    {
        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Activate();
        if (ready && !quitting) { timer.Start(); await RefreshAsync(); }
        if (!string.IsNullOrWhiteSpace(link) && ready && !quitting)
        {
            ShowDownloadsPage();
            await ActAsync(async () => await engine.Client.AddLinkAsync(link, lifetime.Token), "Enlace recibido de otra instancia.");
        }
    }

    private async Task HideToTrayAsync()
    {
        string marker = Path.Combine(engine.ProfilePath, "tray-explained");
        if (!File.Exists(marker))
        {
            var confirm = new ConfirmWindow("Sigue en segundo plano",
                "La ventana se oculta en la bandeja y el motor continúa. Las transferencias no se detienen. Usa «Salir y detener» para cerrar aMule.",
                "Entendido");
            await confirm.ShowDialog(this);
            if (!confirm.Accepted) return;
            File.WriteAllText(marker, "1");
        }
        ShowInTaskbar = false;
        Hide();
        Message.Text = "Oculto en la bandeja. El motor sigue.";
    }

    private async void QuitClicked(object? sender, RoutedEventArgs e) => await QuitAsync();

    private async Task QuitAsync()
    {
        if (quitting) return;
        quitting = true;
        timer.Stop();
        Message.Text = "Guardando la cola y deteniendo el motor…";
        Show();
        ShowInTaskbar = true;
        await operations.WaitAsync();
        try
        {
            await engine.StopAsync();
            lifetime.Cancel();
            SingleInstance.SignalShow();
            if (tray != null)
            {
                tray.IsVisible = false;
                tray.Dispose();
                tray = null;
            }
            allowClose = true;
            Close();
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }
        catch (Exception ex)
        {
            quitting = false;
            Message.Text = "No se pudo cerrar el motor: " + ex.Message + " Puedes volver a intentar salir.";
        }
        finally { operations.Release(); }
    }

    private async void WindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (quitting) return;
        if (Program.CapturePath != null)
        {
            await QuitAsync();
            return;
        }
        await HideToTrayAsync();
    }
}
