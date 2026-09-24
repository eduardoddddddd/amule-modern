using System.Collections.ObjectModel;
using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class ServersWindow : UserControl
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
        InitializeComponent();
        ServerGrid.ItemsSource = servers;
        GridColumns.Attach(ServerGrid, "servers", ["name", "address", "users", "files", "ping"], ColumnsButton);
        UpdateButtons();
    }
    public ServersWindow(EcClient client) : this()
    {
        this.client = client;
        AttachedToVisualTree += async (_, _) =>
        {
            isClosed = false;
            await ExecuteAsync(async () => { available = true; }, null);
            if (available && !isClosed) timer.Start();
            if (available && servers.Count == 0)
                ServerMessage.Text = "Lista vacía. Importa un server.met. La copia se guarda mientras el motor vive y se restaura si el proceso muere.";
        };
        DetachedFromVisualTree += (_, _) => { timer.Stop(); isClosed = true; };
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
        AddOnlyButton.IsEnabled = RefreshServersButton.IsEnabled = ImportFileButton.IsEnabled =
            ImportUrlButton.IsEnabled = ExampleUrlButton.IsEnabled = available && !busy;
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
    private async Task ExecuteAsync(Func<Task> action, string? success)
    {
        if (busy || isClosed) return;
        await gate.WaitAsync(); busy = true; UpdateButtons();
        try { await action(); if (success != null) ServerMessage.Text = success; await RefreshAsync(); }
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
        await confirm.ShowDialog(UiHost.WindowOf(this));
        if (!confirm.Accepted) return;
        await ExecuteAsync(async () => await client.RemoveServerAsync(selected), "Servidor quitado de la lista.");
    }
    private async void ImportFile(object? sender, RoutedEventArgs e)
    {
        try
        {
        var files = await UiHost.StorageOf(this).OpenFilePickerAsync(new FilePickerOpenOptions
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
        await ImportSourceAsync(() => ServerListFile.ReadFileAsync(path));
        }
        catch (Exception ex) { ServerMessage.Text = "No se pudo abrir la lista: " + ex.Message; }
    }
    private static string ImportMessage(int added, int renamed)
    {
        if (added == 0 && renamed == 0) return "Ningún servidor nuevo: las entradas ya estaban en la lista.";
        string part = added == 0 ? "" : added == 1 ? "Importado 1 servidor nuevo" : $"Importados {added} servidores nuevos";
        if (renamed > 0)
        {
            string names = renamed == 1 ? "actualizado 1 nombre" : $"actualizados {renamed} nombres";
            part = part.Length == 0 ? char.ToUpperInvariant(names[0]) + names[1..] : part + "; " + names;
        }
        return part + ".";
    }
    private void UseExampleUrl(object? sender, RoutedEventArgs e)
    {
        ImportUrlInput.Text = ServerListFile.ExampleUrl;
        ServerMessage.Text = "URL de ejemplo lista. Pulsa Importar URL para descargar el server.met.";
    }
    private async void ImportUrl(object? sender, RoutedEventArgs e)
    {
        string url = ImportUrlInput.Text ?? "";
        try
        {
            var uri = ServerListFile.ValidateUrl(url);
            ImportUrlInput.Text = uri.AbsoluteUri;
            ServerMessage.Text = "Descargando " + uri.Host + "…";
        }
        catch (ArgumentException ex) { ServerMessage.Text = ex.Message; return; }
        await ImportSourceAsync(() => ServerListFile.DownloadAsync(ImportUrlInput.Text ?? url));
    }
    private Task ImportBytesAsync(byte[] data) => ImportSourceAsync(() => Task.FromResult(data));
    private async Task ImportSourceAsync(Func<Task<byte[]>> read)
    {
        string? result = null;
        await ExecuteAsync(async () =>
        {
            byte[] data;
            try { data = await read(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException or OperationCanceledException)
            { throw new ArgumentException("No se pudo leer la lista (archivo, conexión o plazo HTTP de 30 s): " + ex.Message, ex); }
            // Only acquisition errors are translated. EC transport failures still disable
            // the disconnected client and cannot be mistaken for an HTTP/file failure.
            result = ImportMessage(await client.ImportServersAsync(ServerListFile.Parse(data)));
        }, "Importación terminada.");
        if (result != null && available) ServerMessage.Text = result;
    }
    private static string ImportMessage((int Added, int Renamed) counts) => ImportMessage(counts.Added, counts.Renamed);
    private async void RefreshClicked(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Task.CompletedTask, "Lista actualizada.");
    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();
    internal async Task ExerciseUiAsync()
    {
        for (int i = 0; i < 100 && (!available || busy); i++) await Task.Delay(50);
        if (!available || busy) throw new InvalidOperationException("La ventana de servidores no está lista.");
        foreach (Exception failure in new Exception[] { new System.Net.Http.HttpRequestException("HTTP 404"), new UnauthorizedAccessException("Access denied"), new OperationCanceledException("deadline") })
        {
            await ImportSourceAsync(() => Task.FromException<byte[]>(failure));
            if (!available || !AddOnlyButton.IsEnabled || !ImportUrlButton.IsEnabled)
                throw new InvalidOperationException("Un error de importación deshabilitó la ventana o el motor.");
        }
        await ImportSourceAsync(() => ServerListFile.ReadFileAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt")));
        if (!available || !RefreshServersButton.IsEnabled) throw new InvalidOperationException("Un archivo desaparecido deshabilitó la conexión EC.");
        await client.GetNetworkStateAsync();
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
        ImportUrlInput.Text = "upd.emule-security.org/server.met";
        // Scheme-less URLs are normalized; do not hit the network in the UI exercise.
        if (ServerListFile.ValidateUrl(ImportUrlInput.Text).Host != "upd.emule-security.org")
            throw new InvalidOperationException("La URL sin https:// no se normaliza.");
        await ImportBytesAsync("203.0.113.34:4661 Importado UI\n"u8.ToArray());
        await gate.WaitAsync(); gate.Release();
        if (!servers.Any(s => s.Address == "203.0.113.34")) throw new InvalidOperationException("Importar archivo no añadió el servidor de prueba.");
        ServerGrid.SelectedItem = servers.First(s => s.Address == "203.0.113.32");
        RemoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var confirm = UiHost.WindowOf(this).OwnedWindows.OfType<ConfirmWindow>().Single();
        confirm.FindControl<Button>("AcceptButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (int i = 0; i < 100 && servers.Any(s => s.Address == "203.0.113.32"); i++) await Task.Delay(50);
        await gate.WaitAsync(); gate.Release();
        if (servers.Any(s => s.Address == "203.0.113.32") || !servers.Any(s => s.Address == "203.0.113.34"))
            throw new InvalidOperationException("Quitar no retiró el servidor o eliminó el importado.");
        ServerMessage.Text = "Prueba superada: validación, añadir, importar, quitar y duplicados. Sin conectar.";
    }
}
