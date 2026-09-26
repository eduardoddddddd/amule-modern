using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Diagnostics;

namespace AmuleModern.Desktop;

public partial class SettingsWindow : UserControl
{
    private readonly EngineSession engine = null!;
    private bool busy;
    public bool IsBusy => busy;
    public SettingsWindow() => InitializeComponent();
    public SettingsWindow(EngineSession engine) : this()
    {
        this.engine = engine;
        IncomingInput.Text = engine.IncomingPath;
        TempInput.Text = engine.TempPath;
        StatusMessage.Text = UserFolders.IsIsolatedProfile(Path.GetFileName(engine.ProfilePath))
            ? "Este perfil de prueba usa carpetas aisladas dentro de .local."
            : $"Por defecto: {UserFolders.Incoming()}";
        AttachedToVisualTree += async (_, _) =>
        {
            IncomingInput.Text = engine.IncomingPath;
            TempInput.Text = engine.TempPath;
            await RefreshLimitsAsync();
            await RefreshKadAsync();
            RefreshEd2k();
        };
    }
    private async Task RefreshLimitsAsync()
    {
        try
        {
            var limits = await engine.Client.GetBandwidthAsync();
            DownloadLimitInput.Text = limits.DownloadKib.ToString();
            UploadLimitInput.Text = limits.UploadKib.ToString();
        }
        catch (Exception ex)
        {
            DownloadLimitInput.Text = "";
            UploadLimitInput.Text = "";
            StatusMessage.Text = "No se pudieron leer los límites: " + ex.Message;
        }
    }
    private static uint ParseLimit(string? text, string label)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return 0;
        if (!uint.TryParse(text, out uint value)) throw new ArgumentException($"El límite de {label} debe ser un número entero.");
        if (value > BandwidthLimits.MaxKib) throw new ArgumentException($"El límite de {label} no puede superar {BandwidthLimits.MaxKib} KiB/s.");
        return value;
    }
    private async void ApplyLimitsClicked(object? sender, RoutedEventArgs e)
    {
        if (busy) return;
        busy = true; ApplyLimitsButton.IsEnabled = false; ApplyButton.IsEnabled = false;
        StatusMessage.Text = "Aplicando límites…";
        try
        {
            await engine.Client.SetBandwidthAsync(ParseLimit(DownloadLimitInput.Text, "bajada"), ParseLimit(UploadLimitInput.Text, "subida"));
            await RefreshLimitsAsync();
            StatusMessage.Text = "Límites aplicados en el motor, sin reinicio. 0 es ilimitado.";
        }
        catch (Exception ex) when (ex is ArgumentException or EcCommandException or IOException or TimeoutException)
        {
            StatusMessage.Text = ex.Message;
        }
        catch (Exception ex) { StatusMessage.Text = "No se pudieron aplicar los límites: " + ex.Message; }
        finally { busy = false; ApplyLimitsButton.IsEnabled = true; ApplyButton.IsEnabled = true; }
    }
    private async Task RefreshKadAsync()
    {
        try
        {
            bool enabled = await engine.Client.GetKadEnabledAsync();
            var network = await engine.Client.GetNetworkStateAsync();
            KadStatus.Text = enabled
                ? $"Kad activado en ajustes. Red: {network.KadText.ToLowerInvariant()}. No se modifica el cortafuegos."
                : "Kad está desactivado. Activarlo usa UDP; no se abre el cortafuegos desde esta app.";
            KadEnableButton.IsEnabled = !enabled;
            KadDisableButton.IsEnabled = enabled;
        }
        catch (Exception ex) { KadStatus.Text = "No se pudo leer Kad: " + ex.Message; }
    }
    private void RefreshEd2k()
    {
        if (!Ed2kProtocol.Supported)
        {
            Ed2kStatus.Text = "En este sistema no se asocia el esquema ed2k://.";
            Ed2kEnableButton.IsEnabled = Ed2kDisableButton.IsEnabled = false;
            return;
        }
        string? exe = Ed2kProtocol.CurrentExecutable();
        if (exe is null)
        {
            Ed2kStatus.Text = "Asocia el esquema desde la aplicación instalada, no desde una ejecución de desarrollo.";
            Ed2kEnableButton.IsEnabled = Ed2kDisableButton.IsEnabled = false;
            return;
        }
        var (owned, previous) = UiSettings.LoadEd2k();
        var current = Ed2kProtocol.Read();
        if (Ed2kAssociation.Owns(current, exe, owned) && !string.Equals(current.Command, Ed2kAssociation.HandlerFor(exe), StringComparison.Ordinal))
        {
            var refresh = Ed2kAssociation.Enable(current, exe, previous, owned);
            Ed2kProtocol.Apply(refresh, exe);
            current = Ed2kProtocol.Read();
            (owned, previous) = UiSettings.LoadEd2k();
        }
        bool ours = Ed2kAssociation.Owns(current, exe, owned);
        Ed2kEnableButton.IsEnabled = !ours;
        Ed2kDisableButton.IsEnabled = ours || previous != null;
        string? warning = OperatingSystem.IsWindows() ? Ed2kProtocol.ForeignDefaultWarning() : null;
        Ed2kStatus.Text = ours
            ? "aMule Modern abre los enlaces ed2k://. Quitar la asociación devuelve el programa anterior, si había uno."
            : "Ningún cambio automático. Al activarlo se recuerda el programa que hubiera y se puede restaurar."
            + (warning is null ? "" : " " + warning);
    }

    private void EnableEd2k(object? sender, RoutedEventArgs e) => ChangeEd2k(true);
    private void DisableEd2k(object? sender, RoutedEventArgs e) => ChangeEd2k(false);

    private void ChangeEd2k(bool enable)
    {
        if (busy) return;
        string? exe = Ed2kProtocol.CurrentExecutable();
        if (exe is null) { Ed2kStatus.Text = "No hay una aplicación instalada que registrar."; return; }
        busy = true;
        try
        {
            var (owned, previous) = UiSettings.LoadEd2k();
            var current = Ed2kProtocol.Read();
            var result = enable
                ? Ed2kAssociation.Enable(current, exe, previous, owned)
                : Ed2kAssociation.Disable(current, exe, previous, owned);
            Ed2kProtocol.Apply(result, exe);
            StatusMessage.Text = result.Message;
        }
        catch (Exception ex) { StatusMessage.Text = ex.Message; }
        finally { busy = false; RefreshEd2k(); }
    }

    private async void EnableKad(object? sender, RoutedEventArgs e) => await SetKadAsync(true);
    private async void DisableKad(object? sender, RoutedEventArgs e) => await SetKadAsync(false);
    private async Task SetKadAsync(bool enabled)
    {
        if (busy) return;
        busy = true; KadEnableButton.IsEnabled = KadDisableButton.IsEnabled = false;
        KadStatus.Text = enabled ? "Activando Kad…" : "Desactivando Kad…";
        try
        {
            await engine.Client.SetKadEnabledAsync(enabled);
            await RefreshKadAsync();
            StatusMessage.Text = enabled
                ? "Kad habilitado en el motor. Conectado solo si hay nodos alcanzables; no se toca el cortafuegos."
                : "Kad desactivado. eD2k no cambia.";
        }
        catch (Exception ex) when (ex is ArgumentException or EcCommandException or IOException or TimeoutException)
        {
            StatusMessage.Text = ex.Message;
            await RefreshKadAsync();
        }
        catch (Exception ex) { StatusMessage.Text = "No se pudo cambiar Kad: " + ex.Message; }
        finally { busy = false; }
    }
    private async void BrowseIncoming(object? sender, RoutedEventArgs e) => IncomingInput.Text = await PickFolderAsync(IncomingInput.Text) ?? IncomingInput.Text;
    private async void BrowseTemp(object? sender, RoutedEventArgs e) => TempInput.Text = await PickFolderAsync(TempInput.Text) ?? TempInput.Text;
    private async Task<string?> PickFolderAsync(string? current)
    {
        var storage = UiHost.StorageOf(this);
        var options = new FolderPickerOpenOptions { Title = "Elige una carpeta", AllowMultiple = false };
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
            options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(current);
        var result = await storage.OpenFolderPickerAsync(options);
        return result.Count == 0 ? null : result[0].TryGetLocalPath();
    }
    private async void ApplyClicked(object? sender, RoutedEventArgs e)
    {
        if (busy) return;
        busy = true; ApplyButton.IsEnabled = false; ApplyLimitsButton.IsEnabled = false;
        StatusMessage.Text = "Deteniendo el motor y aplicando las carpetas…";
        try
        {
            await engine.ApplyDirectoriesAsync(IncomingInput.Text ?? "", TempInput.Text ?? "");
            IncomingInput.Text = engine.IncomingPath;
            TempInput.Text = engine.TempPath;
            StatusMessage.Text = "Motor reiniciado. Incoming: " + engine.IncomingPath;
        }
        catch (Exception ex) when (ex is ArgumentException or EcCommandException or IOException or TimeoutException)
        {
            StatusMessage.Text = ex.Message;
        }
        catch (Exception ex) { StatusMessage.Text = "No se pudieron aplicar las carpetas: " + ex.Message; }
        finally { busy = false; ApplyButton.IsEnabled = true; ApplyLimitsButton.IsEnabled = true; }
    }
    private void OpenIncoming(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path = IncomingInput.Text ?? "";
            if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { StatusMessage.Text = ex.Message; }
    }
    internal void ExerciseUi()
    {
        if (string.IsNullOrWhiteSpace(IncomingInput.Text) || string.IsNullOrWhiteSpace(TempInput.Text))
            throw new InvalidOperationException("Ajustes no muestra las carpetas actuales.");
        if (UserFolders.PathsEqual(IncomingInput.Text!, TempInput.Text!))
            throw new InvalidOperationException("Incoming y tmp no deben coincidir.");
        if (KadStatus == null || KadEnableButton == null) throw new InvalidOperationException("Ajustes no muestra el control de Kad.");
        if (DownloadLimitInput == null || UploadLimitInput == null || ApplyLimitsButton == null)
            throw new InvalidOperationException("Ajustes no muestra los límites de ancho de banda.");
        if (Ed2kEnableButton == null || Ed2kDisableButton == null || string.IsNullOrWhiteSpace(Ed2kStatus.Text))
            throw new InvalidOperationException("Ajustes no muestra la asociación ed2k.");
        StatusMessage.Text = "Prueba de interfaz: rutas, límites, Kad y asociación ed2k visibles.";
    }
}
