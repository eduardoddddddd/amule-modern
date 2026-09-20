using System.Collections.ObjectModel;
using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class ServersWindow : Window
{
    private readonly EcClient client = null!;
    private readonly ObservableCollection<ServerItem> servers = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool busy, available, isClosed;
    private NetworkState? state;
    private string? requestedEndpoint;
    public ServersWindow()
    {
        InitializeComponent(); ServerGrid.ItemsSource = servers; UpdateButtons();
    }
    public ServersWindow(EcClient client) : this()
    {
        this.client = client;
        Opened += async (_, _) =>
        {
            await ExecuteAsync(async () => { await client.EnableEd2kAsync(); available = true; }, "Listo. Añadir guarda el servidor; la conexión comienza solo al pulsar Conectar.");
            if (available && !isClosed) timer.Start();
        };
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Closed += (_, _) => { isClosed = true; timer.Stop(); };
        timer.Tick += async (_, _) =>
        {
            if (busy || isClosed || !await gate.WaitAsync(0)) return;
            try { await RefreshAsync(); }
            catch (Exception ex) { available = false; timer.Stop(); NetworkBadge.Text = "Estado desconocido"; ServerMessage.Text = ex.Message; }
            finally { gate.Release(); UpdateButtons(); }
        };
    }
    private void UpdateButtons()
    {
        if (AddOnlyButton == null) return;
        AddOnlyButton.IsEnabled = RefreshServersButton.IsEnabled = ImportFileButton.IsEnabled = ImportUrlButton.IsEnabled = available && !busy;
        AddConnectButton.IsEnabled = available && !busy && state?.Connecting != true;
        ConnectButton.IsEnabled = available && !busy && state?.Connecting != true && ServerGrid.SelectedItem is ServerItem;
        RemoveButton.IsEnabled = available && !busy && ServerGrid.SelectedItem is ServerItem;
        DisconnectButton.IsEnabled = available && !busy && state?.Connected == true;
    }
    private async Task RefreshAsync()
    {
        var list = await client.GetServersAsync();
        state = await client.GetNetworkStateAsync();
        string? selected = (ServerGrid.SelectedItem as ServerItem)?.Endpoint;
        var endpoints = list.Select(s => s.Endpoint).ToHashSet();
        for (int i = servers.Count - 1; i >= 0; i--) if (!endpoints.Contains(servers[i].Endpoint)) servers.RemoveAt(i);
        foreach (var item in list)
        {
            int index = -1; for (int i = 0; i < servers.Count; i++) if (servers[i].Endpoint == item.Endpoint) { index = i; break; }
            if (index < 0) servers.Add(item); else if (servers[index] != item) servers[index] = item;
        }
        ServerGrid.SelectedItem = servers.FirstOrDefault(s => s.Endpoint == selected);
        EmptyServers.IsVisible = servers.Count == 0;
        NetworkBadge.Text = state.Ed2kText;
        CurrentServer.Text = state.Server is { } current ? $"{current.Name} · {current.Endpoint}" : state.Connecting ? "Esperando respuesta del servidor…" : "Sin servidor conectado";
        if (requestedEndpoint != null && !state.Connecting)
        {
            ServerMessage.Text = state.Connected ? $"Conexión confirmada por el motor: {state.Server?.Endpoint}." : $"No se ha establecido conexión con {requestedEndpoint}. Comprueba dirección, puerto y disponibilidad.";
            requestedEndpoint = null;
        }
        UpdateButtons();
    }
    private async Task ExecuteAsync(Func<Task> action, string success)
    {
        if (busy || isClosed) return;
        await gate.WaitAsync(); busy = true; UpdateButtons();
        try { await action(); ServerMessage.Text = success; await RefreshAsync(); }
        catch (Exception ex) when (ex is ArgumentException or EcCommandException) { ServerMessage.Text = ex.Message; }
        catch (Exception ex) { available = false; timer.Stop(); NetworkBadge.Text = "Estado desconocido"; ServerMessage.Text = "No se pudo consultar el motor: " + ex.Message; }
        finally { busy = false; gate.Release(); UpdateButtons(); }
    }
    private async Task AddAsync(bool connect)
    {
        ServerItem? added = null;
        await ExecuteAsync(async () =>
        {
            added = await client.AddServerAsync(AddressInput.Text ?? "", PortInput.Text ?? "", NameInput.Text ?? "");
            if (connect) { await client.ConnectServerAsync(added); requestedEndpoint = added.Endpoint; }
        }, connect ? "Conexión solicitada. Esperando confirmación del servidor…" : "Servidor guardado. Selecciónalo para conectar.");
        if (added != null) ServerGrid.SelectedItem = servers.FirstOrDefault(s => s.Endpoint == added.Endpoint);
    }
    private async void AddOnly(object? sender, RoutedEventArgs e) => await AddAsync(false);
    private async void AddAndConnect(object? sender, RoutedEventArgs e) => await AddAsync(true);
    private async void ConnectSelected(object? sender, RoutedEventArgs e)
    {
        if (ServerGrid.SelectedItem is ServerItem selected)
            await ExecuteAsync(async () => { await client.ConnectServerAsync(selected); requestedEndpoint = selected.Endpoint; }, "Conexión solicitada. Esperando confirmación del servidor…");
    }
    private async void Disconnect(object? sender, RoutedEventArgs e) => await ExecuteAsync(async () => { await client.DisconnectServerAsync(); }, "Desconexión solicitada al motor.");
    private async void RemoveSelected(object? sender, RoutedEventArgs e)
    {
        if (ServerGrid.SelectedItem is not ServerItem selected) return;
        var confirm = new ConfirmWindow("Quitar servidor",
            $"Se quitará «{selected.Name}» ({selected.Endpoint}) de la lista del perfil. No se borra nada en disco.",
            "Quitar");
        await confirm.ShowDialog(this);
        if (!confirm.Accepted) return;
        await ExecuteAsync(async () => await client.RemoveServerAsync(selected), "Servidor quitado de la lista.");
    }
    private async void ImportFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar lista de servidores",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Listas eD2k") { Patterns = ["*.txt", "*.met", "*.list"] },
                new FilePickerFileType("Todos") { Patterns = ["*.*"] }
            ]
        });
        if (files.Count == 0) return;
        string? path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) { ServerMessage.Text = "No se pudo leer esa ruta."; return; }
        await ImportBytesAsync(File.ReadAllBytes(path));
    }
    private static string ImportMessage(int added) => added == 0
        ? "Ningún servidor nuevo: las entradas ya estaban en la lista."
        : added == 1 ? "Importado 1 servidor nuevo." : $"Importados {added} servidores nuevos.";
    private async void ImportUrl(object? sender, RoutedEventArgs e)
    {
        string url = ImportUrlInput.Text ?? "";
        string? result = null;
        await ExecuteAsync(async () =>
        {
            byte[] data = await ServerListFile.DownloadAsync(url);
            result = ImportMessage(await client.ImportServersAsync(ServerListFile.Parse(data)));
        }, "Importación desde URL terminada.");
        if (result != null && available) ServerMessage.Text = result;
    }
    private async Task ImportBytesAsync(byte[] data)
    {
        string? result = null;
        await ExecuteAsync(async () => { result = ImportMessage(await client.ImportServersAsync(ServerListFile.Parse(data))); }, "Importación de archivo terminada.");
        if (result != null && available) ServerMessage.Text = result;
    }
    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Task.CompletedTask, "Lista actualizada.");
    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();
    internal async Task ExerciseUiAsync()
    {
        for (int i = 0; i < 100 && (!available || busy); i++) await Task.Delay(50);
        if (!available || busy) throw new InvalidOperationException("La ventana de servidores no está lista.");
        NameInput.Text = "Servidor de prueba · sin conexión";
        AddressInput.Text = "203.0.113.32"; PortInput.Text = "70000";
        AddOnlyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await gate.WaitAsync(); gate.Release();
        if (!ServerMessage.Text!.Contains("65535")) throw new InvalidOperationException("No se valida el puerto.");
        PortInput.Text = "4661";
        AddOnlyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await gate.WaitAsync(); gate.Release();
        if (!servers.Any(s => s.Address == "203.0.113.32") || !ConnectButton.IsEnabled)
            throw new InvalidOperationException("Añadir no actualizó la lista o su selección.");
        AddOnlyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await gate.WaitAsync(); gate.Release();
        if (servers.Count(s => s.Address == "203.0.113.32") != 1 || state?.Connected != false)
            throw new InvalidOperationException("Duplicado inesperado o conexión automática.");
        ImportUrlInput.Text = "file:///C:/servers.txt";
        ImportUrlButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await gate.WaitAsync(); gate.Release();
        if (ServerMessage.Text is null || !ServerMessage.Text.Contains("http", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Importar URL no rechaza file://.");
        await ImportBytesAsync("203.0.113.34:4661 Importado UI\n"u8.ToArray());
        await gate.WaitAsync(); gate.Release();
        if (!servers.Any(s => s.Address == "203.0.113.34")) throw new InvalidOperationException("Importar archivo no añadió el servidor de prueba.");
        ServerGrid.SelectedItem = servers.First(s => s.Address == "203.0.113.32");
        RemoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var confirm = OwnedWindows.OfType<ConfirmWindow>().Single();
        confirm.FindControl<Button>("AcceptButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (int i = 0; i < 100 && servers.Any(s => s.Address == "203.0.113.32"); i++) await Task.Delay(50);
        await gate.WaitAsync(); gate.Release();
        if (servers.Any(s => s.Address == "203.0.113.32") || !servers.Any(s => s.Address == "203.0.113.34"))
            throw new InvalidOperationException("Quitar no retiró el servidor o eliminó el importado.");
        ServerMessage.Text = "Prueba superada: validación, añadir, importar, quitar y duplicados. Sin conectar.";
    }
}
