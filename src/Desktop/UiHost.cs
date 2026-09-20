using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AmuleModern.Desktop;

internal static class UiHost
{
    public static Window WindowOf(Control control) =>
        TopLevel.GetTopLevel(control) as Window
        ?? throw new InvalidOperationException("La vista no está dentro de la ventana principal.");

    public static IStorageProvider StorageOf(Control control) =>
        TopLevel.GetTopLevel(control)?.StorageProvider
        ?? throw new InvalidOperationException("No hay proveedor de archivos disponible.");
}
