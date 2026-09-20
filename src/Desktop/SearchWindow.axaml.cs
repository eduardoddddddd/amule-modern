using System.Collections.ObjectModel;
using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class SearchWindow : UserControl
{
    private readonly EcClient client = null!;
    private readonly ObservableCollection<SearchResult> rows = [];
    private IReadOnlyList<SearchResult> snapshot = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool busy, available, connected, kadConnected, searching, searchingKad, closed;
    public event Action? GoToDownloads;
    public SearchWindow() { InitializeComponent(); ResultsGrid.ItemsSource = rows; }
    public SearchWindow(EcClient client) : this()
    {
        this.client = client;
        AttachedToVisualTree += async (_, _) =>
        {
            // Leaving the page sets closed; returning must reopen or clicks silently no-op.
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
    private void UpdateButtons()
    {
        if (SearchButton == null) return;
        SearchButton.IsEnabled = available && !busy && ((ScopeInput?.SelectedIndex ?? 0) == 2 ? kadConnected : connected);
        StopButton.IsEnabled = available && !busy && searching;
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
        snapshot = await client.GetSearchResultsAsync(); ApplyFilter();
    }
    private void UpdateNetworkState(NetworkState network)
    {
        connected = network.Connected; kadConnected = network.KadConnected;
        NetworkLabel.Text = connected
            ? $"Servidor: {network.Server?.Name} · {network.Ed2kText}   |   Kad: {network.KadText}"
            : kadConnected
                ? $"Sin eD2k. Kad: {network.KadText}"
                : "Sin conexión eD2k ni Kad. Conecta desde Servidores o activa Kad en Ajustes.";
        if (searching && !(searchingKad ? kadConnected : connected)) searching = false;
        UpdateButtons();
    }
    private void ApplyFilter()
    {
        if (FilterInput == null) return;
        var selected = ResultsGrid.SelectedItems.Cast<SearchResult>().Select(r => r.Hash).ToHashSet();
        var filtered = snapshot.Where(r => r.Name.Contains(FilterInput.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        var hashes = filtered.Select(r => r.Hash).ToHashSet();
        for (int i = rows.Count - 1; i >= 0; i--) if (!hashes.Contains(rows[i].Hash)) rows.RemoveAt(i);
        foreach (var result in filtered)
        {
            var previous = rows.FirstOrDefault(r => r.Hash == result.Hash);
            if (previous == null) rows.Add(result);
            else if (previous != result) rows[rows.IndexOf(previous)] = result;
        }
        foreach (var row in rows.Where(r => selected.Contains(r.Hash))) if (!ResultsGrid.SelectedItems.Contains(row)) ResultsGrid.SelectedItems.Add(row);
        ResultCount.Text = $"{rows.Count} RESULTADOS · {snapshot.Count} recibidos";
        EmptyResults.IsVisible = rows.Count == 0;
        EmptyResults.Text = searching ? "Esperando resultados del servidor…" : "Sin resultados. Escribe un nombre y pulsa Buscar.";
        UpdateButtons();
    }
    private async void SearchClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        bool useKad = ScopeInput.SelectedIndex == 2;
        await client.StartSearchAsync(QueryInput.Text ?? "", ScopeInput.SelectedIndex == 1, useKad);
        searchingKad = useKad;
        snapshot = []; rows.Clear(); searching = true;
        StatusMessage.Text = "Búsqueda enviada. Los resultados se actualizan solos. Puedes lanzar otra búsqueda o detenerla.";
        await RefreshAsync();
        if (!searchingKad && !connected)
            StatusMessage.Text = "Sin conexión eD2k. Conecta un servidor y vuelve a buscar.";
        else if (searchingKad && !kadConnected)
            StatusMessage.Text = "Kad no está conectado. Actívalo en Ajustes o usa el servidor eD2k.";
        else if (snapshot.Count == 0)
            StatusMessage.Text = searching
                ? "Búsqueda en curso. Si no aparecen resultados en unos segundos, prueba Global eD2k o comprueba el servidor."
                : StatusMessage.Text;
    });
    private async void StopClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    { await client.StopSearchAsync(); searching = false; await RefreshAsync(); StatusMessage.Text = "Búsqueda detenida. Se conservan los resultados recibidos."; });
    private async void DownloadClicked(object? sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var selected = ResultsGrid.SelectedItems.Cast<SearchResult>().ToArray();
        int count = 0;
        foreach (var result in selected) { await client.DownloadSearchResultAsync(result.Hash); count++; StatusMessage.Text = $"{count} archivo(s) confirmados en la cola de descargas."; }
    });
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
        // Inject connection states into the UI only; no Kad packets or public peers.
        var actualNetwork = await client.GetNetworkStateAsync();
        searching = true; searchingKad = true; ScopeInput.SelectedIndex = 2;
        UpdateNetworkState(new NetworkState(false, false, true, true, null, null));
        if (!StopButton.IsEnabled || !SearchButton.IsEnabled) throw new InvalidOperationException("Kad sin eD2k pierde el botón Detener.");
        ScopeInput.SelectedIndex = 0;
        UpdateNetworkState(new NetworkState(false, false, true, true, null, null));
        if (!StopButton.IsEnabled) throw new InvalidOperationException("Cambiar el ámbito pierde la búsqueda Kad activa.");
        UpdateNetworkState(new NetworkState(false, false, false, false, null, null));
        if (StopButton.IsEnabled) throw new InvalidOperationException("Detener sigue activo tras desconectar la red de la búsqueda.");
        UpdateNetworkState(actualNetwork);
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
    }
}
