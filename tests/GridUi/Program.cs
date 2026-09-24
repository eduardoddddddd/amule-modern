using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using AmuleModern.Desktop;

var app = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
return app;

static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<GridCheckApp>().UsePlatformDetect().WithInterFont().LogToTrace();

public sealed class GridCheckApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new InvalidOperationException("Falta el ciclo de escritorio.");
        string dir = Path.Combine(Path.GetTempPath(), "amule-grid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        UiSettings.FilePathOverride = Path.Combine(dir, "ui.json");
        UiSettings.SaveTheme("Light");

        var grid = CreateGrid();
        var button = new Button { Content = "Columnas" };
        var window = new Window
        {
            Title = "Columnas",
            Width = 900,
            Height = 420,
            Content = new StackPanel { Children = { button, grid } }
        };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                GridColumns.Attach(grid, "downloads", ["name", "size", "state"], button);
                await Task.Delay(700);
                if (File.ReadAllText(UiSettings.Path).Contains("downloads"))
                    throw new InvalidOperationException("La tabla se guardó sin que el usuario cambiara una columna.");
                grid.Columns[2].DisplayIndex = 0;
                grid.Columns[1].Width = new DataGridLength(240);
                grid.Columns[1].IsVisible = false;
                await Task.Delay(700);
                var saved = UiSettings.LoadColumns("downloads", ["name", "size", "state"]);
                if (!saved.Select(c => c.Id).SequenceEqual(["state", "name", "size"]))
                    throw new InvalidOperationException("El orden guardado no es state, name, size: " + string.Join(", ", saved.Select(c => c.Id)));
                if (saved[1].Id != "name" || !saved[1].Visible)
                    throw new InvalidOperationException("La primera columna dejó de verse.");
                var size = saved.Single(c => c.Id == "size");
                if (size.Visible || size.WidthUnit != "Pixel" || Math.Abs(size.WidthValue - 240) > 0.1)
                    throw new InvalidOperationException("No se recordó el ancho ni la columna oculta.");
                if (UiSettings.LoadTheme() != "Light")
                    throw new InvalidOperationException("Guardar columnas borró el tema.");

                var again = CreateGrid();
                var host = new Window { Width = 900, Height = 420, Content = again, ShowInTaskbar = false };
                host.Show();
                GridColumns.Attach(again, "downloads", ["name", "size", "state"], new Button());
                string order = string.Join(", ", again.Columns.OrderBy(c => c.DisplayIndex).Select(c => c.Header));
                if (order != "ESTADO, ARCHIVO, TAMAÑO")
                    throw new InvalidOperationException("La tabla nueva no restauró el orden: " + order);
                if (again.Columns[1].IsVisible || Math.Abs(again.Columns[1].Width.Value - 240) > 0.1)
                    throw new InvalidOperationException("La tabla nueva no restauró ancho ni visibilidad.");

                GridColumns.SetDensity("Compact");
                if (!grid.Classes.Contains("compact") || !again.Classes.Contains("compact") || UiSettings.LoadDensity() != "Compact")
                    throw new InvalidOperationException("La densidad compacta no llegó a las dos tablas.");
                if (UiSettings.LoadTheme() != "Light")
                    throw new InvalidOperationException("Cambiar la densidad borró el tema.");
                Console.WriteLine("PASS: grid restores order, width, visibility and density.");
                desktop.Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                desktop.Shutdown(1);
            }
            finally
            {
                UiSettings.FilePathOverride = null;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        };
        base.OnFrameworkInitializationCompleted();
    }

    private static DataGrid CreateGrid()
    {
        var grid = new DataGrid { AutoGenerateColumns = false, Height = 240, Width = 860 };
        grid.Columns.Add(new DataGridTextColumn { Header = "ARCHIVO", Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "TAMAÑO", Width = new DataGridLength(100) });
        grid.Columns.Add(new DataGridTextColumn { Header = "ESTADO", Width = new DataGridLength(120) });
        grid.ItemsSource = new[] { new Row("Uno", "1 KB", "En espera") };
        return grid;
    }

    private sealed record Row(string Name, string SizeText, string StateText);
}
