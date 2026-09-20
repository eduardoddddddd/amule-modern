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
    public static string FindRepository()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "docs", "engine-manifest.json"))) return dir.FullName;
        throw new DirectoryNotFoundException("Ejecuta esta versión desde el repositorio amule-modern.");
    }
    public async Task StartAsync(string repository, string profileName, CancellationToken token = default)
    {
        if (process != null) throw new InvalidOperationException("La sesión ya tiene un proceso.");
        if (profileName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Perfil inválido.");
        this.repository = repository;
        this.profileName = profileName;
        ProfilePath = Path.Combine(repository, ".local", profileName);
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
                if (UserFolders.IsUnder(IncomingPath, ProfilePath)) IncomingPath = UserFolders.Incoming();
                if (UserFolders.IsUnder(TempPath, ProfilePath)) TempPath = UserFolders.Temp();
                UserFolders.CopyContents(Path.Combine(ProfilePath, "Incoming"), IncomingPath);
                UserFolders.CopyContents(Path.Combine(ProfilePath, "Temp"), TempPath);
            }
        }
        Directory.CreateDirectory(IncomingPath);
        Directory.CreateDirectory(TempPath);
        string original = File.ReadAllText(config);
        var settings = new Dictionary<string, string>
        {
            ["ConnectToED2K"] = "1", ["Autoconnect"] = "0", ["Reconnect"] = "0",
            ["Ed2kServersUrl"] = "", ["Serverlist"] = "0", ["NewVersionCheck"] = "0",
            ["IncomingDir"] = UserFolders.ForConfig(IncomingPath),
            ["TempDir"] = UserFolders.ForConfig(TempPath)
        };
        WriteSettings(config, original, settings);
        var probe = new TcpListener(IPAddress.Loopback, Port);
        probe.Start(); probe.Stop();
        string engine = Path.Combine(repository, "vendor", "amule-3.0.1", "amule-portable-x64", "bin", "amuled.exe");
        if (!File.Exists(engine)) throw new FileNotFoundException("Ejecuta scripts/Setup.ps1 para obtener el motor.");
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
            try { await Client.ConnectAsync(Port, hash, token); return; }
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
        incoming = ValidateDirectory(incoming, "Incoming");
        temp = ValidateDirectory(temp, "temporales");
        if (UserFolders.PathsEqual(incoming, temp)) throw new ArgumentException("Incoming y temporales no pueden ser la misma carpeta.");
        string repo = repository, name = profileName;
        if (string.IsNullOrEmpty(repo)) throw new InvalidOperationException("El motor no está iniciado.");
        await StopAsync();
        IncomingPath = incoming;
        TempPath = temp;
        Directory.CreateDirectory(incoming);
        Directory.CreateDirectory(temp);
        string config = Path.Combine(ProfilePath, "amule.conf");
        WriteSettings(config, File.ReadAllText(config), new Dictionary<string, string>
        {
            ["IncomingDir"] = UserFolders.ForConfig(incoming),
            ["TempDir"] = UserFolders.ForConfig(temp)
        });
        await StartAsync(repo, name, token);
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
