using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace AmuleModern.Desktop;

internal static class GridColumns
{
    private static readonly List<Session> Sessions = [];

    public static void Attach(DataGrid grid, string tableId, string[] ids, Button chooser)
    {
        if (ids.Length != grid.Columns.Count) throw new ArgumentException("Los identificadores no coinciden con las columnas de " + tableId + ".");
        var session = new Session(grid, tableId, ids);
        Sessions.Add(session);
        session.Attach(chooser);
    }

    public static void SetDensity(string density)
    {
        string value = density == "Compact" ? "Compact" : "Comfortable";
        UiSettings.SaveDensity(value);
        foreach (var session in Sessions) session.ApplyDensity(value);
    }

    private sealed class Session
    {
        private readonly DataGrid grid;
        private readonly string tableId;
        private readonly string[] ids;
        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
        private readonly Dictionary<DataGridColumn, string> columnIds = [];
        private bool applying;

        public Session(DataGrid grid, string tableId, string[] ids)
        {
            this.grid = grid;
            this.tableId = tableId;
            this.ids = ids;
            timer.Tick += (_, _) => { timer.Stop(); Save(); };
        }

        public void Attach(Button chooser)
        {
            grid.CanUserReorderColumns = true;
            grid.CanUserResizeColumns = true;
            for (int i = 0; i < ids.Length; i++)
            {
                var column = grid.Columns[i];
                columnIds[column] = ids[i];
                column.PropertyChanged += (_, change) =>
                {
                    if (applying) return;
                    if (change.Property == DataGridColumn.WidthProperty || change.Property == DataGridColumn.IsVisibleProperty || change.Property.Name == nameof(DataGridColumn.DisplayIndex))
                        Schedule();
                };
            }
            Apply(UiSettings.LoadColumns(tableId, ids));
            ApplyDensity(UiSettings.LoadDensity());
            var flyout = new Flyout();
            flyout.Opened += (_, _) => flyout.Content = BuildChooser();
            chooser.Flyout = flyout;
        }

        public void ApplyDensity(string density)
        {
            if (density == "Compact") grid.Classes.Add("compact");
            else grid.Classes.Remove("compact");
        }

        private Control BuildChooser()
        {
            var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(12) };
            foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex))
            {
                string id = columnIds[column];
                bool required = id == ids[0];
                var box = new CheckBox
                {
                    Content = column.Header?.ToString() ?? id,
                    IsChecked = column.IsVisible || required,
                    IsEnabled = !required
                };
                box.IsCheckedChanged += (_, _) =>
                {
                    if (applying) return;
                    bool show = box.IsChecked == true || required;
                    if (!show && grid.Columns.Count(c => c.IsVisible && c != column) == 0)
                    {
                        applying = true;
                        box.IsChecked = true;
                        applying = false;
                        return;
                    }
                    column.IsVisible = show;
                };
                panel.Children.Add(box);
            }
            return panel;
        }

        private void Apply(IReadOnlyList<ColumnLayout> layout)
        {
            applying = true;
            try
            {
                var byId = columnIds.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
                foreach (var item in layout)
                {
                    var column = byId[item.Id];
                    column.IsVisible = item.Visible;
                    if (ToLength(item) is DataGridLength width) column.Width = width;
                }
                for (int target = 0; target < layout.Count; target++)
                {
                    var column = byId[layout[target].Id];
                    if (column.DisplayIndex != target) column.DisplayIndex = target;
                }
            }
            finally { applying = false; }
        }

        private void Schedule()
        {
            timer.Stop();
            timer.Start();
        }

        private void Save()
        {
            if (applying) return;
            var captured = grid.Columns.Select(column =>
            {
                var width = column.Width;
                return new ColumnLayout(columnIds[column], column.DisplayIndex, column.IsVisible, width.Value, width.UnitType.ToString());
            }).ToArray();
            UiSettings.SaveColumns(tableId, ColumnLayoutRules.Normalize(captured, ids));
        }

        private static DataGridLength? ToLength(ColumnLayout item) => item.WidthUnit switch
        {
            "Pixel" => new DataGridLength(item.WidthValue),
            "Star" => new DataGridLength(item.WidthValue, DataGridLengthUnitType.Star),
            "Auto" => DataGridLength.Auto,
            "SizeToCells" => DataGridLength.SizeToCells,
            "SizeToHeader" => DataGridLength.SizeToHeader,
            _ => null
        };
    }
}
