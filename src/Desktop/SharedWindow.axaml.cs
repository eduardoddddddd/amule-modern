using System.Collections.ObjectModel;
using System.Diagnostics;
using AmuleModern.Amule;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

public partial class SharedWindow : UserControl
{
    private readonly EngineSession engine = null!;
    private readonly ObservableCollection<SharedFile> rows = [];
    private IReadOnlyList<SharedFile> snapshot = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(4) };
    private bool busy, available, closed;
    private string[] extraFolders = [];
    private readonly ObservableCollection<string> folderOptions = [];
    public SharedWindow()
    {
        InitializeComponent();
        SharedGrid.ItemsSource = rows;
        GridColumns.Attach(SharedGrid, "shared", ["name", "size", "folder", "requests"], ColumnsButton);
    }
    public SharedWindow(EngineSession engine) : this()
    {
        this.engine = engine;
        ExtraFoldersInput.ItemsSource = folderOptions;
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
    private void UpdateButtons()
    {
        if (AddFolderButton == null) return;
        AddFolderButton.IsEnabled = ReloadButton.IsEnabled = available && !busy;
        bool selected = SharedGrid.SelectedItem is SharedFile;
        CopyLinkButton.IsEnabled = selected && available && !busy;
        OpenFolderButton.IsEnabled = selected && available && !busy;
        RemoveFolderButton.IsEnabled = available && !busy && ExtraFoldersInput.SelectedItem is string;
    }
    private void Fail(Exception ex) { available = false; timer.Stop(); StatusMessage.Text = "No se pudo consultar el motor: " + ex.Message; }
    private async Task RunAsync(Func<Task> action, string? success = null)
    {
        if (busy || closed) return;
        await gate.WaitAsync(); busy = true; UpdateButtons();
        try { await action(); if (success != null) StatusMessage.Text = success; }
        catch (Exception ex) when (ex is ArgumentException or EcCommandException) { StatusMessage.Text = ex.Message; }
        catch (Exception ex) { Fail(ex); }
        finally { busy = false; gate.Release(); UpdateButtons(); }
    }
    private async Task RefreshAsync()
    {
        snapshot = await engine.Client.GetSharedFilesAsync();
        extraFolders = engine.ExtraSharedDirectories()
            .Where(path => !UserFolders.PathsEqual(path, engine.IncomingPath))
            .ToArray();
        FoldersText.Text = "Incoming se comparte solo: " + engine.IncomingPath;
        string? selectedFolder = ExtraFoldersInput.SelectedItem as string;
        for (int i = folderOptions.Count - 1; i >= 0; i--)
            if (!extraFolders.Any(folder => UserFolders.PathsEqual(folder, folderOptions[i]))) folderOptions.RemoveAt(i);
        foreach (string folder in extraFolders)
            if (!folderOptions.Any(existing => UserFolders.PathsEqual(existing, folder))) folderOptions.Add(folder);
        ExtraFoldersInput.SelectedItem = folderOptions.FirstOrDefault(folder => selectedFolder != null && UserFolders.PathsEqual(folder, selectedFolder));
        CountBadge.Text = snapshot.Count == 1 ? "1 archivo" : snapshot.Count + " archivos";
        ApplyFilter();
    }
    private void ApplyFilter()
    {
        if (FilterInput == null) return;
        string? selected = (SharedGrid.SelectedItem as SharedFile)?.Hash;
        var filtered = snapshot.Where(f => f.Name.Contains(FilterInput.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        var hashes = filtered.Select(f => f.Hash).ToHashSet();
        for (int i = rows.Count - 1; i >= 0; i--) if (!hashes.Contains(rows[i].Hash)) rows.RemoveAt(i);
        foreach (var item in filtered)
        {
            int index = -1; for (int i = 0; i < rows.Count; i++) if (rows[i].Hash == item.Hash) { index = i; break; }
            if (index < 0) rows.Add(item); else if (rows[index] != item) rows[index] = item;
        }
        if (selected != null) SharedGrid.SelectedItem = rows.FirstOrDefault(r => r.Hash == selected);
        EmptyShared.IsVisible = rows.Count == 0;
        UpdateButtons();
    }
    private async void AddFolder(object? sender, RoutedEventArgs e)
    {
        var storage = UiHost.StorageOf(this);
        var options = new FolderPickerOpenOptions { Title = "Carpeta a compartir (solo esa carpeta, no el disco entero)", AllowMultiple = false };
        if (Directory.Exists(engine.IncomingPath))
            options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(engine.IncomingPath);
        var picked = await storage.OpenFolderPickerAsync(options);
        if (picked.Count == 0) return;
        string? path = picked[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        await RunAsync(async () =>
        {
            var next = extraFolders.Append(path).ToArray();
            await engine.ApplyExtraSharedDirectoriesAsync(next);
            await RefreshAsync();
        }, "Carpeta añadida. El motor vuelve a leer los archivos; el hash puede tardar unos segundos.");
    }
    private async void RemoveFolder(object? sender, RoutedEventArgs e)
    {
        if (ExtraFoldersInput.SelectedItem is not string folder) return;
        var confirm = new ConfirmWindow("Dejar de compartir",
            $"Se dejará de ofrecer la carpeta «{folder}». Los archivos no se borran del disco.",
            "Dejar de compartir");
        await confirm.ShowDialog(UiHost.WindowOf(this));
        if (!confirm.Accepted) return;
        await RunAsync(async () =>
        {
            await engine.RemoveExtraSharedDirectoryAsync(folder);
            await RefreshAsync();
        }, "Carpeta extra retirada. Incoming sigue compartido.");
    }
    private async void CopyLink(object? sender, RoutedEventArgs e)
    {
        if (SharedGrid.SelectedItem is not SharedFile file) return;
        string link = file.Ed2kLink;
        if (string.IsNullOrWhiteSpace(link))
            link = $"ed2k://|file|{Uri.EscapeDataString(file.Name)}|{file.Size}|{file.Hash}|/";
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(link);
        StatusMessage.Text = "Enlace copiado.";
    }
    private void OpenFolder(object? sender, RoutedEventArgs e)
    {
        if (SharedGrid.SelectedItem is not SharedFile file) return;
        OpenPath(file.FolderText == "—" ? engine.IncomingPath : file.FolderText);
    }
    private void OpenIncoming(object? sender, RoutedEventArgs e) => OpenPath(engine.IncomingPath);
    private void OpenPath(string path)
    {
        try { if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { StatusMessage.Text = ex.Message; }
    }
    private async void ReloadClicked(object? sender, RoutedEventArgs e) =>
        await RunAsync(async () => { await engine.Client.ReloadSharedFilesAsync(); await RefreshAsync(); }, "Lista recargada desde el motor.");
    private void FilterChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();
    private void SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();
    internal async Task ExerciseUiAsync()
    {
        for (int i = 0; i < 100 && (!available || busy); i++) await Task.Delay(50);
        if (!available) throw new InvalidOperationException("La ventana de compartidos no está lista.");
        string emptyFolder = Path.Combine(engine.ProfilePath, "ui-empty-share-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyFolder);
        await RunAsync(async () => { await engine.ApplyExtraSharedDirectoriesAsync(extraFolders.Append(emptyFolder).ToArray()); await RefreshAsync(); });
        ExtraFoldersInput.SelectedItem = emptyFolder;
        if (!RemoveFolderButton.IsEnabled || rows.Any(row => UserFolders.IsUnder(row.Path, emptyFolder)))
            throw new InvalidOperationException("No se puede seleccionar una carpeta vacía sin seleccionar archivos.");
        RemoveFolderButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var confirm = UiHost.WindowOf(this).OwnedWindows.OfType<ConfirmWindow>().Single();
        confirm.FindControl<Button>("AcceptButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (int i = 0; i < 100 && engine.ExtraSharedDirectories().Contains(emptyFolder); i++) await Task.Delay(50);
        await gate.WaitAsync(); gate.Release();
        if (engine.ExtraSharedDirectories().Contains(emptyFolder) || !Directory.Exists(emptyFolder))
            throw new InvalidOperationException("Quitar carpeta vacía no retiró la entrada o borró el directorio.");
        if (string.IsNullOrWhiteSpace(FoldersText.Text) || !FoldersText.Text.Contains("Incoming", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Compartidos no indica Incoming.");
        FilterInput.Text = "no-coincide-compartidos-xyz";
        await Task.Delay(50);
        if (rows.Count != 0) throw new InvalidOperationException("El filtro no oculta filas.");
        FilterInput.Text = "";
        StatusMessage.Text = "Prueba superada: quitar carpeta vacía desde su selector, confirmar y conservar el directorio. Lista y filtro correctos.";
    }
}
