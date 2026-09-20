using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace AmuleModern.Amule;

// Initial development host: owns exactly the process it starts, never attaches to an unknown one.
public sealed class EngineSession : IAsyncDisposable
{
    private Process? process;
    private FileStream? lease;
    private string repository = "";
    private string profileName = "";
    public EcClient Client { get; private set; } = new();
    public string ProfilePath { get; private set; } = "";
    public string IncomingPath { get; private set; } = "";
    public string TempPath { get; private set; } = "";
    public int Port { get; private set; }
    public int? ProcessId => process is { HasExited: false } ? process.Id : null;
    public sealed record AppLayout(string Root, string EnginePath)
    {
        public bool IsRepository => File.Exists(Path.Combine(Root, "docs", "engine-manifest.json"));
    }
    public static string FindRepository() => FindLayout().Root;
    public static AppLayout FindLayout() => Locate(AppContext.BaseDirectory);
    public static AppLayout Locate(string startDirectory)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(startDirectory)); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "engine-manifest.json")))
                return new(dir.FullName, Path.Combine(dir.FullName, "vendor", "amule-3.0.1", "amule-portable-x64", "bin", "amuled.exe"));
            if (File.Exists(Path.Combine(dir.FullName, "engine-manifest.json")))
                return new(dir.FullName, Path.Combine(dir.FullName, "engine", "bin", "amuled.exe"));
        }
        throw new DirectoryNotFoundException("No se encontró el motor. Ejecuta scripts/Setup.ps1, el paquete portable o la instalación por usuario.");
    }
    public async Task StartAsync(string repository, string profileName, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (process != null) throw new InvalidOperationException("La sesión ya tiene un proceso.");
        if (profileName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Perfil inválido.");
        var layout = Locate(repository);
        this.repository = layout.Root;
        this.profileName = profileName;
        ProfilePath = Path.Combine(layout.Root, ".local", profileName);
        Directory.CreateDirectory(ProfilePath);
        if (OperatingSystem.IsWindows())
        {
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            var sid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Usuario Windows no disponible.");
            acl.SetOwner(sid);
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(ProfilePath).SetAccessControl(acl);
        }
        lease = new FileStream(Path.Combine(ProfilePath, "frontend.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string config = Path.Combine(ProfilePath, "amule.conf");
        string hash;
        bool isolated = UserFolders.IsIsolatedProfile(profileName);
        if (!File.Exists(config))
        {
            Port = FreePort();
            hash = EcClient.Md5Hex(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            IncomingPath = isolated ? Path.Combine(ProfilePath, "Incoming") : UserFolders.Incoming();
            TempPath = isolated ? Path.Combine(ProfilePath, "Temp") : UserFolders.Temp();
            Directory.CreateDirectory(IncomingPath);
            Directory.CreateDirectory(TempPath);
            string text = $"[eMule]\nNick=AmuleModern\nAppVersion=3.0.1\nIncomingDir={UserFolders.ForConfig(IncomingPath)}\nTempDir={UserFolders.ForConfig(TempPath)}\nPort={FreePort()}\nUDPPort={FreePort()}\nAutoconnect=0\nReconnect=0\nConnectToKad=0\nConnectToED2K=0\nUPnPEnabled=0\n[ExternalConnect]\nAcceptExternalConnections=1\nECAddress=127.0.0.1\nECPort={Port}\nECPassword={hash}\n[WebServer]\nEnabled=0\n";
            File.WriteAllText(config, text, new UTF8Encoding(false));
        }
        else
        {
            var lines = File.ReadAllLines(config);
            Port = int.Parse(lines.Single(l => l.StartsWith("ECPort=", StringComparison.Ordinal))[7..]);
            hash = lines.Single(l => l.StartsWith("ECPassword=", StringComparison.Ordinal))[11..];
            IncomingPath = ReadDirectory(lines, "IncomingDir=", isolated ? Path.Combine(ProfilePath, "Incoming") : UserFolders.Incoming());
            TempPath = ReadDirectory(lines, "TempDir=", isolated ? Path.Combine(ProfilePath, "Temp") : UserFolders.Temp());
            if (!isolated)
            {
                bool migrateIncoming = UserFolders.IsUnder(IncomingPath, ProfilePath);
                bool migrateTemp = UserFolders.IsUnder(TempPath, ProfilePath);
                if (migrateIncoming) IncomingPath = UserFolders.Incoming();
                if (migrateTemp) TempPath = UserFolders.Temp();
                UserFolders.MigrateLegacyProfileOnce(ProfilePath, IncomingPath, TempPath, migrateIncoming, migrateTemp);
            }
        }
        Directory.CreateDirectory(IncomingPath);
        Directory.CreateDirectory(TempPath);
        string original = File.ReadAllText(config);
        var settings = new Dictionary<string, string>
        {
            ["ConnectToED2K"] = "1", ["Autoconnect"] = "0", ["Reconnect"] = "0",
            ["Ed2kServersUrl"] = "", ["Serverlist"] = "0", ["NewVersionCheck"] = "0",
            ["RemoveDeadServer"] = "0", ["IPFilterAutoLoad"] = "0",
            ["AddServerListFromServer"] = "0", ["AddServerListFromClient"] = "0",
            ["IncomingDir"] = UserFolders.ForConfig(IncomingPath),
            ["TempDir"] = UserFolders.ForConfig(TempPath)
        };
        WriteSettings(config, original, settings);
        var probe = new TcpListener(IPAddress.Loopback, Port);
        probe.Start(); probe.Stop();
        string engine = layout.EnginePath;
        if (!File.Exists(engine)) throw new FileNotFoundException(layout.IsRepository
            ? "Ejecuta scripts/Setup.ps1 para obtener el motor."
            : "Falta amuled.exe junto a la aplicación (carpeta engine/bin).");
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(engine)!, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-c"); start.ArgumentList.Add(ProfilePath);
        start.ArgumentList.Add("-o");
        process = Process.Start(start) ?? throw new IOException("No se pudo iniciar amuled.");
        process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        for (int attempt = 0; attempt < 50; attempt++)
        {
            token.ThrowIfCancellationRequested();
            if (process.HasExited) throw new IOException($"amuled terminó ({process.ExitCode}). Revisa el logfile del perfil.");
            Client.Dispose(); Client = new EcClient();
            try { await Client.ConnectAsync(Port, hash, token); await Client.EnableEd2kAsync(token); return; }
            catch (SocketException) { await Task.Delay(200, token); }
        }
        throw new TimeoutException("amuled no abrió la conexión EC a tiempo.");
    }
    public async Task ReconnectAsync(CancellationToken token = default)
    {
        if (process is null || process.HasExited) throw new IOException("El motor no está en ejecución.");
        string hash = File.ReadAllLines(Path.Combine(ProfilePath, "amule.conf")).Single(l => l.StartsWith("ECPassword=", StringComparison.Ordinal))[11..];
        Client.Dispose();
        Client = new EcClient();
        await Client.ConnectAsync(Port, hash, token);
    }
    public async Task ApplyDirectoriesAsync(string incoming, string temp, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        incoming = ValidateDirectory(incoming, "Incoming");
        temp = ValidateDirectory(temp, "temporales");
        if (UserFolders.PathsEqual(incoming, temp)) throw new ArgumentException("Incoming y temporales no pueden ser la misma carpeta.");
        string repo = repository, name = profileName;
        if (string.IsNullOrEmpty(repo)) throw new InvalidOperationException("El motor no está iniciado.");
        if (UserFolders.PathsEqual(incoming, IncomingPath) && UserFolders.PathsEqual(temp, TempPath)) return;
        if (!UserFolders.PathsEqual(temp, TempPath) && (await Client.GetDownloadsAsync(token)).Any(d => !d.IsComplete))
            throw new ArgumentException("No puedes cambiar temporales mientras haya descargas pendientes, incluidas las pausadas. Termínalas o cancélalas antes; sus archivos se conservan en la carpeta actual.");
        // Check destinations before interrupting a working motor.
        CheckWritableDirectory(incoming);
        CheckWritableDirectory(temp);
        await StopAsync();
        string config = Path.Combine(ProfilePath, "amule.conf");
        string previousConfig = File.ReadAllText(config);
        try
        {
            WriteSettings(config, previousConfig, new Dictionary<string, string>
            {
                ["IncomingDir"] = UserFolders.ForConfig(incoming),
                ["TempDir"] = UserFolders.ForConfig(temp)
            });
            await StartAsync(repo, name, token);
        }
        catch (Exception error)
        {
            try
            {
                await StopAsync();
                File.WriteAllText(config, previousConfig, new UTF8Encoding(false));
                await StartAsync(repo, name, CancellationToken.None);
            }
            catch (Exception rollback) { throw new IOException("Falló el cambio de carpetas y no se pudo restaurar el motor: " + rollback.Message, new AggregateException(error, rollback)); }
            throw new IOException("No se aplicaron las carpetas. Se restauraron la configuración anterior y el motor: " + error.Message, error);
        }
    }
    private static void CheckWritableDirectory(string path)
    {
        Directory.CreateDirectory(path);
        string probe = Path.Combine(path, ".amule-write-check-" + Guid.NewGuid().ToString("N"));
        using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }
    public async Task ApplyExtraSharedDirectoriesAsync(IReadOnlyList<string> directories, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(ProfilePath)) throw new InvalidOperationException("El motor no está iniciado.");
        var unique = new List<string>();
        foreach (string raw in directories)
        {
            string path = ValidateExtraShared(raw);
            if (unique.Any(existing => UserFolders.PathsEqual(existing, path))) continue;
            unique.Add(path);
        }
        if (unique.Count > 32) throw new ArgumentException("Como máximo 32 carpetas extra.");
        WriteExplicitShared(unique);
        await Client.ReloadSharedFilesAsync(token);
    }
    public IReadOnlyList<string> ExtraSharedDirectories()
    {
        string file = Path.Combine(ProfilePath, "shareddir-explicit.dat");
        if (!File.Exists(file)) return [];
        return File.ReadAllLines(file)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(UserFolders.FromConfig)
            .ToArray();
    }
    public async Task RemoveExtraSharedDirectoryAsync(string directory, CancellationToken token = default)
    {
        // Removing a saved entry must work even when it no longer exists on disk,
        // including when other saved directories have also become unavailable.
        WriteExplicitShared(ExtraSharedDirectories().Where(path => !UserFolders.PathsEqual(path, directory)).ToArray());
        await Client.ReloadSharedFilesAsync(token);
    }
    public string ValidateExtraShared(string path)
    {
        path = ValidateDirectory(path, "compartida");
        if (!Directory.Exists(path)) throw new ArgumentException("La carpeta a compartir debe existir.");
        if (UserFolders.PathsEqual(path, IncomingPath)) throw new ArgumentException("Incoming ya se comparte automáticamente.");
        if (UserFolders.PathsEqual(path, TempPath) || UserFolders.IsUnder(path, TempPath) || UserFolders.IsUnder(TempPath, path))
            throw new ArgumentException("No se comparte la carpeta de temporales.");
        if (UserFolders.PathsEqual(path, ProfilePath) || UserFolders.IsUnder(ProfilePath, path))
            throw new ArgumentException("No se comparte el perfil del motor ni una carpeta que lo contenga.");
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if ((!string.IsNullOrEmpty(windows) && (UserFolders.PathsEqual(path, windows) || UserFolders.IsUnder(path, windows))) ||
            (!string.IsNullOrEmpty(programs) && (UserFolders.PathsEqual(path, programs) || UserFolders.IsUnder(path, programs))))
            throw new ArgumentException("No se pueden compartir carpetas del sistema.");
        if (path.TrimEnd('\\', '/').Length <= 3) throw new ArgumentException("No se comparte una unidad completa.");
        return path;
    }
    private void WriteExplicitShared(IReadOnlyList<string> directories)
    {
        var utf8 = new UTF8Encoding(false);
        string body = string.Join("\n", directories.Select(path => Path.GetFullPath(path)));
        if (directories.Count > 0) body += "\n";
        // aMule 3.0.1 keeps shareddir-explicit.dat only if the path also
        // appears in shareddir.dat; external writers must update the union.
        File.WriteAllText(Path.Combine(ProfilePath, "shareddir-explicit.dat"), body, utf8);
        File.WriteAllText(Path.Combine(ProfilePath, "shareddir-recursive.dat"), "", utf8);
        File.WriteAllText(Path.Combine(ProfilePath, "shareddir.dat"), body, utf8);
    }
    public static string ValidateDirectory(string path, string label)
    {
        path = (path ?? "").Trim();
        if (path.Length is < 2 or > 240) throw new ArgumentException($"La carpeta {label} no es válida.");
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) throw new ArgumentException($"La carpeta {label} contiene caracteres no válidos.");
        path = Path.GetFullPath(path);
        if (!Path.IsPathRooted(path)) throw new ArgumentException($"La carpeta {label} debe ser una ruta absoluta.");
        return path;
    }
    private static string ReadDirectory(string[] lines, string key, string fallback)
    {
        string? line = lines.FirstOrDefault(l => l.StartsWith(key, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(line) || line.Length <= key.Length) return fallback;
        try { return UserFolders.FromConfig(line[key.Length..]); }
        catch (Exception) { return fallback; }
    }
    private static void WriteSettings(string config, string original, Dictionary<string, string> settings)
    {
        var configLines = original.Replace("\r\n", "\n").Split('\n').ToList();
        int section = configLines.IndexOf("[eMule]");
        if (section < 0) throw new InvalidDataException("amule.conf no tiene sección [eMule].");
        int end = configLines.FindIndex(section + 1, line => line.StartsWith('['));
        if (end < 0) end = configLines.Count;
        foreach (var setting in settings)
        {
            int index = configLines.FindIndex(section + 1, end - section - 1, line => line.StartsWith(setting.Key + "=", StringComparison.Ordinal));
            if (index >= 0) configLines[index] = setting.Key + "=" + setting.Value;
            else { configLines.Insert(end++, setting.Key + "=" + setting.Value); }
        }
        string updated = string.Join("\n", configLines);
        if (original == updated) return;
        if (!File.Exists(config + ".pre-servers.bak")) File.Copy(config, config + ".pre-servers.bak");
        File.WriteAllText(config, updated, new UTF8Encoding(false));
    }
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }
    public async Task StopAsync()
    {
        if (process is { HasExited: false })
        {
            Client.Dispose(); Client = new EcClient();
            string hash = File.ReadAllLines(Path.Combine(ProfilePath, "amule.conf")).Single(l => l.StartsWith("ECPassword=", StringComparison.Ordinal))[11..];
            await Client.ConnectAsync(Port, hash);
            await Client.RequestAsync(new(8));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(deadline.Token);
        }
        Client.Dispose(); process?.Dispose(); process = null;
        lease?.Dispose(); lease = null;
    }
    public async ValueTask DisposeAsync() => await StopAsync();
}
