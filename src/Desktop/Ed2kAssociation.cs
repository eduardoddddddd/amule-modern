namespace AmuleModern.Desktop;

internal sealed record ProtocolSnapshot(bool Exists, string? Description, string? Command, bool UrlProtocol);

internal enum AssociationAction { None, WriteOurs, Restore, Delete }

internal sealed record AssociationResult(
    AssociationAction Action,
    ProtocolSnapshot? Restore,
    ProtocolSnapshot? Previous,
    string? OwnedCommand,
    string Message);

internal static class Ed2kAssociation
{
    public const string Description = "URL:ed2k Protocol";

    public static string QuoteCommand(string exePath) => $"\"{exePath}\" \"%1\"";

    public static string HandlerFor(string exeOrBundle) =>
        exeOrBundle.Contains('/') || exeOrBundle.Contains('\\') || exeOrBundle.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? QuoteCommand(exeOrBundle)
            : exeOrBundle;

    public static string? ExecutableFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
        int space = command.IndexOf(' ');
        return space < 0 ? command : command[..space];
    }

    public static bool SameExecutable(string? command, string exePath)
    {
        string? exe = ExecutableFromCommand(command);
        if (string.IsNullOrWhiteSpace(exe) || string.IsNullOrWhiteSpace(exePath)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(exe), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(exe, exePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static bool Owns(ProtocolSnapshot current, string exePath, string? ownedCommand) =>
        SameExecutable(current.Command, exePath)
        || (ownedCommand != null && string.Equals(current.Command, ownedCommand, StringComparison.OrdinalIgnoreCase));

    public static AssociationResult Enable(ProtocolSnapshot current, string exePath, ProtocolSnapshot? previous, string? ownedCommand)
    {
        string ours = HandlerFor(exePath);
        if (Owns(current, exePath, ownedCommand))
        {
            bool rewrite = !string.Equals(current.Command, ours, StringComparison.Ordinal);
            return new(rewrite ? AssociationAction.WriteOurs : AssociationAction.None, null, previous, ours,
                "Los enlaces ed2k:// se abren con aMule Modern.");
        }
        return new(AssociationAction.WriteOurs, null, current, ours,
            current.Exists
                ? "Se recordó el programa anterior. Al quitar la asociación vuelve ese programa."
                : "Los enlaces ed2k:// se abren con aMule Modern.");
    }

    public static AssociationResult Disable(ProtocolSnapshot current, string exePath, ProtocolSnapshot? previous, string? ownedCommand)
    {
        if (!Owns(current, exePath, ownedCommand))
            return new(AssociationAction.None, null, null, null, "Otro programa tiene el enlace. No se ha cambiado.");
        if (previous is { Exists: true })
            return new(AssociationAction.Restore, previous, null, null, "Asociación quitada. Volvió el programa anterior.");
        return new(AssociationAction.Delete, null, null, null, "Asociación quitada.");
    }
}
