using System.Collections.ObjectModel;
using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class SearchWindow : UserControl
{
    private const int MaxTabs = 12;
    private readonly EcClient client = null!;
    private readonly ObservableCollection<SearchResult> rows = [];
    private readonly List<SearchTab> tabs = [];
    private SearchTab? current;
    private IReadOnlyList<SearchResult> snapshot = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool busy, available, connected, kadConnected, closed;
    public event Action? GoToDownloads;

    private sealed class SearchTab
    {
        public required string Query { get; init; }
        public required string ScopeLabel { get; init; }
        public required bool UsedKad { get; init; }
        public List<SearchResult> Results { get; set; } = [];
        public string Filter { get; set; } = "";
        public bool IsLive { get; set; }
        public bool Searching { get; set; }
        public string Title
        {
            get
            {
                string q = Query.Length <= 22 ? Query : Query[..21] + "…";
                return Searching ? $"● {q}" : q;
            }
        }
    }

    public SearchWindow()
    {
        InitializeComponent();
        ResultsGrid.ItemsSource = rows;
        GridColumns.Attach(ResultsGrid, "search", ["name", "size", "sources", "complete"], ColumnsButton);
    }
    public SearchWindow(EcClient client) : this()
    {
        this.client = client;
        AttachedToVisualTree += async (_, _) =>
        {
            closed = false;
            await RunAsync(async () => { available = true; await RefreshAsync(); });
            if (available && !closed) timer.Start();
        };
        DetachedFromVisualTree += (_, _) => { timer.Stop(); closed = true; };
        timer.Tick += async (_, _) =>
        {
            if (closed || busy || !await gate.WaitAsync(0)) return;
            try { await RefreshAsync(); }
            catch (Exception ex) { Fail(ex); }
            finally { gate.Release(); UpdateButtons(); }
        };
    }

    private bool LiveSearching => current is { IsLive: true, Searching: true };

    private void UpdateButtons()
    {
        if (SearchButton == null) return;
        SearchButton.IsEnabled = available && !busy && ((ScopeInput?.SelectedIndex ?? 0) == 2 ? kadConnected : connected);
        StopButton.IsEnabled = available && !busy && LiveSearching;
        DownloadButton.IsEnabled = available && !busy && ResultsGrid.SelectedItems.Count > 0;
    }

    private void Fail(Exception ex) { available = false; timer.Stop(); StatusMessage.Text = "No se pudo consultar el motor: " + ex.Message; }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy || closed) return;
        await gate.WaitAsync(); busy = true; UpdateButtons();
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or EcCommandException) { StatusMessage.Text = ex.Message; }
        catch (Exception ex) { Fail(ex); }
        finally { busy = false; gate.Release(); UpdateButtons(); }
    }

    private async Task RefreshAsync()
    {
        var network = await client.GetNetworkStateAsync();
        UpdateNetworkState(network);
        if (current is { IsLive: true })
        {
            snapshot = await client.GetSearchResultsAsync();
            current.Results = snapshot.ToList();
            if (current.Searching && !(current.UsedKad ? kadConnected : connected))
                current.Searching = false;
        }
        else
            snapshot = current?.Results ?? [];
        ApplyFilter();
        RebuildTabBar();
    }

    private void UpdateNetworkState(NetworkState network)
    {
        connected = network.Connected; kadConnected = network.KadConnected;
        NetworkLabel.Text = connected
            ? $"Servidor: {network.Server?.Name} · {network.Ed2kText}   |   Kad: {network.KadText}"
            : kadConnected
                ? $"Sin eD2k. Kad: {network.KadText}"
                : "Sin conexión eD2k ni Kad. Conecta desde Servidores o activa Kad en Ajustes.";
        if (current is { Searching: true } && !(current.UsedKad ? kadConnected : connected))
            current.Searching = false;
        UpdateButtons();
    }

    private void ApplyFilter()
    {
        if (FilterInput == null) return;
        var selected = ResultsGrid.SelectedItems.Cast<SearchResult>().Select(r => r.Hash).ToHashSet();
        string filter = FilterInput.Text ?? "";
        if (current != null) current.Filter = filter;
        var filtered = snapshot.Where(r => r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        var hashes = filtered.Select(r => r.Hash).ToHashSet();
        for (int i = rows.Count - 1; i >= 0; i--) if (!hashes.Contains(rows[i].Hash)) rows.RemoveAt(i);
        foreach (var result in filtered)
        {
            var previous = rows.FirstOrDefault(r => r.Hash == result.Hash);
            if (previous == null) rows.Add(result);
            else if (previous != result) rows[rows.IndexOf(previous)] = result;
        }
        foreach (var row in rows.Where(r => selected.Contains(r.Hash))) if (!ResultsGrid.SelectedItems.Contains(row)) ResultsGrid.SelectedItems.Add(row);
        string tabHint = current == null ? "" : current.IsLive ? (current.Searching ? " · en curso" : " · activa") : " · guardada";
        ResultCount.Text = $"{rows.Count} RESULTADOS · {snapshot.Count} recibidos{tabHint}";
        EmptyResults.IsVisible = rows.Count == 0;
        EmptyResults.Text = current == null
            ? "Escribe un nombre y pulsa Buscar. Cada búsqueda se guarda en una pestaña."
            : current.Searching ? "Esperando resultados del servidor…"
            : snapshot.Count == 0 ? "Esta pestaña no recibió resultados."
            : "Sin coincidencias para este filtro.";
        UpdateButtons();
    }

    private void RebuildTabBar()
    {
        if (TabBar == null) return;
        TabBar.Children.Clear();
        foreach (var tab in tabs)
        {
            var chip = new Border
            {
                Classes = { "panel" },
                Padding = new Avalonia.Thickness(2, 2, 2, 2),
                CornerRadius = new Avalonia.CornerRadius(8),
                Background = ReferenceEquals(tab, current)
                    ? this.FindResource("App.NavSelected") as IBrush
                    : this.FindResource("App.Elevated") as IBrush
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            var open = new Button
            {
                Content = $"{tab.Title}  ·  {tab.ScopeLabel}",
                Classes = { "ghost" },
                Padding = new Avalonia.Thickness(10, 6),
                FontWeight = ReferenceEquals(tab, current) ? FontWeight.SemiBold : FontWeight.Normal,
                Tag = tab
            };
            open.Click += TabOpenClicked;
            var close = new Button
            {
                Content = "×",
                Classes = { "ghost" },
                Padding = new Avalonia.Thickness(8, 6),
                Tag = tab
            };
            ToolTip.SetTip(close, "Cerrar pestaña");
            close.Click += TabCloseClicked;
            row.Children.Add(open);
            row.Children.Add(close);
            chip.Child = row;
            TabBar.Children.Add(chip);
        }
    }

    private void SelectTab(SearchTab tab)
    {
        if (ReferenceEquals(current, tab)) return;
        if (current != null) current.Filter = FilterInput.Text ?? "";
        current = tab;
        FilterInput.Text = tab.Filter;
        snapshot = tab.Results;
        rows.Clear();
        ApplyFilter();
        RebuildTabBar();
        StatusMessage.Text = tab.IsLive
            ? (tab.Searching ? "Pestaña activa: recibiendo resultados del motor." : "Pestaña activa del motor. Puedes descargar o lanzar otra búsqueda.")
            : "Pestaña guardada (instantánea). La descarga usa el enlace ed2k de cada resultado.";
        UpdateButtons();
    }

    private void TabOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SearchTab tab }) SelectTab(tab);
    }

    private async void TabCloseClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SearchTab tab }) return;
        await RunAsync(async () =>
        {
            if (tab.IsLive && tab.Searching)
            {
                await client.StopSearchAsync();
                tab.Searching = false;
            }
            int index = tabs.IndexOf(tab);
            tabs.Remove(tab);
            if (ReferenceEquals(current, tab))
            {
                current = null;
                if (tabs.Count > 0) SelectTab(tabs[Math.Clamp(index, 0, tabs.Count - 1)]);
                else
                {
                    snapshot = [];
                    rows.Clear();
                    FilterInput.Text = "";
                    ApplyFilter();
                    RebuildTabBar();
                }
            }
            else RebuildTabBar();
            if (tab.IsLive)
            {
                foreach (var other in tabs) other.IsLive = false;
            }
            StatusMessage.Text = "Pestaña cerrada.";
        });
    }

    private async Task FreezeLiveTabAsync()
    {
        var live = tabs.FirstOrDefault(t => t.IsLive);
        if (live == null) return;
        live.Results = (await client.GetSearchResultsAsync()).ToList();
        live.Searching = false;
        live.IsLive = false;
    }

    private async void SearchClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        string query = (QueryInput.Text ?? "").Trim();
        bool useKad = ScopeInput.SelectedIndex == 2;
        bool global = ScopeInput.SelectedIndex == 1;
        string scope = useKad ? "Kad" : global ? "Global" : "Servidor";
        await FreezeLiveTabAsync();
        while (tabs.Count >= MaxTabs)
            tabs.RemoveAt(0);
        var tab = new SearchTab
        {
            Query = query,
            ScopeLabel = scope,
            UsedKad = useKad,
            IsLive = true,
            Searching = true,
            Results = []
        };
        tabs.Add(tab);
        current = tab;
        FilterInput.Text = "";
        snapshot = [];
        rows.Clear();
        RebuildTabBar();
        await client.StartSearchAsync(query, global, useKad);
        StatusMessage.Text = "Búsqueda enviada en una pestaña nueva. Las anteriores se conservan.";
        await RefreshAsync();
        if (!useKad && !connected)
            StatusMessage.Text = "Sin conexión eD2k. Conecta un servidor y vuelve a buscar.";
        else if (useKad && !kadConnected)
            StatusMessage.Text = "Kad no está conectado. Actívalo en Ajustes o usa el servidor eD2k.";
        else if (snapshot.Count == 0 && tab.Searching)
            StatusMessage.Text = "Búsqueda en curso. Si no aparecen resultados, prueba Global eD2k o revisa el servidor.";
    });

    private async void StopClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (current is not { IsLive: true }) { StatusMessage.Text = "Solo se puede detener la pestaña activa del motor."; return; }
        await client.StopSearchAsync();
        current.Searching = false;
        await RefreshAsync();
        StatusMessage.Text = "Búsqueda detenida. La pestaña conserva los resultados recibidos.";
    });

    private async void DownloadClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var selected = ResultsGrid.SelectedItems.Cast<SearchResult>().ToArray();
        int count = 0;
        bool live = current is { IsLive: true };
        foreach (var result in selected)
        {
            if (live)
                await client.DownloadSearchResultAsync(result.Hash);
            else
                await client.AddLinkAsync(Ed2kLink(result));
            count++;
            StatusMessage.Text = $"{count} archivo(s) confirmados en la cola de descargas.";
        }
    });

    private static string Ed2kLink(SearchResult result)
    {
        string name = result.Name.Replace("|", "_", StringComparison.Ordinal);
        return $"ed2k://|file|{Uri.EscapeDataString(name).Replace("%20", " ", StringComparison.Ordinal)}|{result.Size}|{result.Hash}|/";
    }

    private void QueryKeyDown(object? sender, KeyEventArgs e) { if (e.Key == Key.Enter) SearchClicked(sender, new RoutedEventArgs()); }
    private void ResultDoubleTapped(object? sender, TappedEventArgs e) { if (DownloadButton.IsEnabled) DownloadClicked(sender, new RoutedEventArgs()); }
    private void ScopeChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();
    private void FilterChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();
    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();
    private void ShowDownloads(object? sender, RoutedEventArgs e) => GoToDownloads?.Invoke();

    internal async Task ExerciseUiAsync()
    {
        for (int i = 0; i < 100 && (!available || busy); i++) await Task.Delay(50);
        if (!available) throw new InvalidOperationException("La ventana de búsqueda no está lista.");
        var actualNetwork = await client.GetNetworkStateAsync();
        var probe = new SearchTab { Query = "kad-probe", ScopeLabel = "Kad", UsedKad = true, IsLive = true, Searching = true };
        tabs.Clear();
        tabs.Add(probe);
        current = probe;
        ScopeInput.SelectedIndex = 2;
        UpdateNetworkState(new NetworkState(false, false, true, true, null, null));
        RebuildTabBar();
        if (!StopButton.IsEnabled || !SearchButton.IsEnabled) throw new InvalidOperationException("Kad sin eD2k pierde el botón Detener.");
        ScopeInput.SelectedIndex = 0;
        UpdateNetworkState(new NetworkState(false, false, true, true, null, null));
        if (!StopButton.IsEnabled) throw new InvalidOperationException("Cambiar el ámbito pierde la búsqueda Kad activa.");
        UpdateNetworkState(new NetworkState(false, false, false, false, null, null));
        if (StopButton.IsEnabled) throw new InvalidOperationException("Detener sigue activo tras desconectar la red de la búsqueda.");
        tabs.Clear();
        current = null;
        UpdateNetworkState(actualNetwork);
        RebuildTabBar();
        if (!connected)
        {
            if (SearchButton.IsEnabled) throw new InvalidOperationException("Buscar no debe activarse sin eD2k.");
            if (!NetworkLabel.Text!.Contains("Servidores", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Sin conexión no se indica ir a Servidores.");
            StatusMessage.Text = "Prueba superada: estados simulados Kad sin eD2k conservan Detener. Búsqueda real cubierta en integración controlada.";
            return;
        }
        QueryInput.Text = "ubuntu";
        SearchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await gate.WaitAsync(); gate.Release();
        for (int i = 0; i < 40 && rows.Count == 0; i++) await Task.Delay(500);
        if (rows.Count == 0) throw new InvalidOperationException("Buscar no devolvió resultados reales.");
        if (tabs.Count != 1) throw new InvalidOperationException("La primera búsqueda no creó una pestaña.");
        FilterInput.Text = "no-match-8b197afe";
        await Task.Delay(100);
        if (rows.Count != 0) throw new InvalidOperationException("El filtro no excluye resultados.");
        FilterInput.Text = ""; await Task.Delay(100);
        if (rows.Count == 0) throw new InvalidOperationException("El filtro no restaura resultados.");
        ResultsGrid.SelectedItem = rows[0];
        if (!DownloadButton.IsEnabled) throw new InvalidOperationException("No se puede descargar el resultado seleccionado.");
        StopButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await gate.WaitAsync(); gate.Release();
        if (StopButton.IsEnabled || rows.Count == 0) throw new InvalidOperationException("Detener no conserva los resultados.");
        QueryInput.Text = "linux";
        SearchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await gate.WaitAsync(); gate.Release();
        if (tabs.Count != 2) throw new InvalidOperationException("La segunda búsqueda no conservó la pestaña anterior.");
        SelectTab(tabs[0]);
        if (tabs[0].Results.Count == 0) throw new InvalidOperationException("La pestaña anterior perdió sus resultados.");
        StatusMessage.Text = "Prueba superada: pestañas de búsqueda, filtro y detener. Búsqueda real cubierta en integración.";
    }
}
