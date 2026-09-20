using AmuleModern.Amule;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

if (args.Contains("--live-search"))
{
    var config = File.ReadAllLines(Path.Combine(EngineSession.FindRepository(), ".local", "desktop", "amule.conf"));
    using var live = new EcClient();
    await live.ConnectAsync(int.Parse(config.Single(l => l.StartsWith("ECPort="))[7..]), config.Single(l => l.StartsWith("ECPassword="))[11..]);
    var network = await live.GetNetworkStateAsync();
    Console.WriteLine($"NETWORK: {network.Ed2kText}; SERVER: {network.Server?.Endpoint}");
    await live.StartSearchAsync("ubuntu");
    for (int i = 0; i < 12; i++)
    {
        await Task.Delay(1000);
        var results = await live.GetSearchResultsAsync();
        if (results.Count > 0) { Console.WriteLine($"LIVE SEARCH: {results.Count} results; first: {results[0].Name}; sources: {results[0].Sources}"); await live.StopSearchAsync(); return; }
    }
    throw new Exception("El servidor no devolvió resultados en 12 segundos.");
}

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
void Reject(byte[] body, string name)
{
    try { EcProtocol.DecodeBody(body); throw new Exception("Accepted invalid packet: " + name); }
    catch (InvalidDataException) { Check(true, name); }
}

// Independent reference bytes from EC stats request: fixed 8-byte big-endian header.
var stats = EcProtocol.Encode(new(0x0a, EcTag.Integer(4, 0)));
Check(Convert.ToHexString(stats) == "000000200000000B0A00010008020000000100", "golden EC stats request");
// Parent TAGLEN excludes its OWN child-count but includes all child wire bytes.
var nestedBytes = Convert.FromHexString("0700010601090000001F0001060206000000086578616D706C6500000102030405060708090A0B0C0D0E0F");
var nested = EcProtocol.DecodeBody(nestedBytes);
Check(nested.Tags[0].Data.Length == 16 && nested.Tags[0].Find(0x301)?.String == "example", "independent nested tag fixture");
Check(EcProtocol.Encode(nested).AsSpan(8).SequenceEqual(nestedBytes), "nested packet wire encoding");
Reject([0x0c, 0, 1], "truncated tag");
Reject([0x0c, 0, 0, 0], "trailing packet data");
var badLength = (byte[])nestedBytes.Clone(); badLength[9] = 1;
Reject(badLength, "parent shorter than its children");
Check(UserFolders.Incoming().Replace('\\', '/').EndsWith("amule-modern/incoming", StringComparison.OrdinalIgnoreCase)
    && UserFolders.Temp().Replace('\\', '/').EndsWith("amule-modern/tmp", StringComparison.OrdinalIgnoreCase),
    "user library lives under Downloads/amule-modern");

// TCP response split into individual bytes, not a single ReadAsync-sized packet.
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
int fakePort = ((IPEndPoint)listener.LocalEndpoint).Port;
async Task<EcPacket> ReadPacket(NetworkStream stream)
{
    byte[] header = new byte[8]; await stream.ReadExactlyAsync(header);
    byte[] body = new byte[BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4))];
    await stream.ReadExactlyAsync(body); return EcProtocol.DecodeBody(body);
}
var fakeServer = Task.Run(async () =>
{
    using var peer = await listener.AcceptTcpClientAsync(); var stream = peer.GetStream();
    _ = await ReadPacket(stream);
    foreach (byte b in EcProtocol.Encode(new(0x4f, EcTag.Integer(0xb, 0xabcUL, 8)))) await stream.WriteAsync(new byte[] { b });
    var password = await ReadPacket(stream);
    var expected = Convert.FromHexString(EcClient.Md5Hex(EcClient.Md5Hex("test") + EcClient.Md5Hex("ABC")));
    Check(password.Find(1)!.Data.SequenceEqual(expected), "salt without leading zero padding");
    await stream.WriteAsync(EcProtocol.Encode(new(4)));
});
using (var fakeClient = new EcClient()) await fakeClient.ConnectAsync(fakePort, EcClient.Md5Hex("test"));
await fakeServer; listener.Stop();
Check(true, "fragmented TCP authentication");

if (args.Contains("--integration"))
{
    var root = EngineSession.FindRepository();
    string profile = "integration-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    const string hash = "A448017AAF21D8525FC10AE87AA6729D";
    const string link = "ed2k://|file|amule-modern-test-abc.txt|3|A448017AAF21D8525FC10AE87AA6729D|/";
    const string incomingShare = "amule-modern-share-in.txt";
    string extraDir = Path.Combine(root, ".local", profile, "share-extra");
    await using (var engine = new EngineSession())
    {
        await engine.StartAsync(root, profile);
        Check(engine.ProcessId.HasValue, "real amuled process and EC auth");
        Check(UserFolders.IsUnder(engine.IncomingPath, engine.ProfilePath) && UserFolders.IsUnder(engine.TempPath, engine.ProfilePath), "integration folders stay inside the isolated profile");
        Console.WriteLine($"ENGINE: {engine.Client.ServerVersion}; EC: 127.0.0.1:{engine.Port}");
        using (var wrong = new EcClient())
        {
            bool rejected = false;
            try { await wrong.ConnectAsync(engine.Port, EcClient.Md5Hex("wrong")); }
            catch (UnauthorizedAccessException) { rejected = true; }
            Check(rejected, "real engine rejects wrong password");
        }
        Check((await engine.Client.GetDownloadsAsync()).Count == 0, "isolated empty queue");
        Check((await engine.Client.GetSharedFilesAsync()).Count == 0, "isolated empty shared list");
        File.WriteAllText(Path.Combine(engine.IncomingPath, incomingShare), "amule-modern-share-incoming-payload");
        await engine.Client.ReloadSharedFilesAsync();
        bool incomingShared = false;
        for (int i = 0; i < 40; i++)
        {
            incomingShared = (await engine.Client.GetSharedFilesAsync()).Any(f => f.Name == incomingShare);
            if (incomingShared) break;
            await Task.Delay(250);
        }
        Check(incomingShared, "incoming file appears in shared list after reload");
        Directory.CreateDirectory(extraDir);
        File.WriteAllText(Path.Combine(extraDir, "amule-modern-share-extra.txt"), "xyz");
        await engine.ApplyExtraSharedDirectoriesAsync([extraDir]);
        Check(engine.ExtraSharedDirectories().Any(d => UserFolders.PathsEqual(d, extraDir)), "extra shared directory persisted");
        bool extraShared = false;
        for (int i = 0; i < 40; i++)
        {
            extraShared = (await engine.Client.GetSharedFilesAsync()).Any(f => f.Name == "amule-modern-share-extra.txt");
            if (extraShared) break;
            await Task.Delay(250);
        }
        Check(extraShared, "extra folder file appears in shared list");
        bool rejectedIncoming = false, rejectedTemp = false, rejectedProfile = false;
        try { engine.ValidateExtraShared(engine.IncomingPath); } catch (ArgumentException) { rejectedIncoming = true; }
        try { engine.ValidateExtraShared(engine.TempPath); } catch (ArgumentException) { rejectedTemp = true; }
        try { engine.ValidateExtraShared(engine.ProfilePath); } catch (ArgumentException) { rejectedProfile = true; }
        Check(rejectedIncoming && rejectedTemp && rejectedProfile, "reject Incoming, Temp and the engine profile as extra shares");
        await engine.ApplyExtraSharedDirectoriesAsync([]);
        Check(!engine.ExtraSharedDirectories().Any(d => UserFolders.PathsEqual(d, extraDir)), "removing extra shared directory");
        Check(!(await engine.Client.GetKadEnabledAsync()) && !(await engine.Client.GetNetworkStateAsync()).KadRunning, "Kad starts disabled");
        Check((await engine.Client.GetDownloadsAsync()).Count == 0, "sharing does not enqueue downloads");
        await engine.Client.AddLinkAsync(link);
        var downloads = await engine.Client.GetDownloadsAsync();
        Check(downloads.Any(d => d.Hash == hash && d.Size == 3), "add link and read real queue");
        await engine.Client.PauseAsync(hash, true);
        Check((await engine.Client.GetDownloadsAsync()).Single().State == 7, "pause real download");
        await engine.Client.PauseAsync(hash, false);
        Check((await engine.Client.GetDownloadsAsync()).Single().State != 7, "resume real download");
        await engine.Client.PauseAsync(hash, true);
        int? pid = engine.ProcessId;
        await engine.ReconnectAsync();
        Check(engine.ProcessId == pid && pid.HasValue, "EC reconnect keeps the same amuled process");
        Check((await engine.Client.GetDownloadsAsync()).Any(d => d.Hash == hash && d.State == 7), "queue readable after EC reconnect");
        const string extraHash = "C448017AAF21D8525FC10AE87AA6729D";
        await engine.Client.AddLinkAsync("ed2k://|file|amule-modern-test-def.txt|3|C448017AAF21D8525FC10AE87AA6729D|/");
        var queued = await engine.Client.GetDownloadsAsync();
        Check(queued.Count == 2 && queued.All(d => d.EcId != 0), "full queue detail includes session ECID");
        await engine.Client.PauseAsync(queued.Select(d => d.Hash).ToArray(), true);
        Check((await engine.Client.GetDownloadsAsync()).All(d => d.State == 7), "pause several downloads in one EC command");
        await engine.Client.CancelDownloadsAsync([extraHash]);
        Check((await engine.Client.GetDownloadsAsync()).Select(d => d.Hash).Single() == hash, "cancel removes one download and keeps the other");
        Check((await engine.Client.RequestAsync(new(0x0a, EcTag.Integer(4, 0)))).Operation == 0x0c, "real stats request");
        await engine.Client.EnableEd2kAsync();
        var prefs = await engine.Client.RequestAsync(new(0x3f, EcTag.Integer(0x1000, 4), EcTag.Integer(4, 2)));
        var connectionPrefs = prefs.Find(0x1300)!;
        Check(connectionPrefs.Find(0x130d) != null && connectionPrefs.Find(0x130e) == null && connectionPrefs.Find(0x130b) == null,
            "enable eD2k without enabling Kad or autoconnect");
        await engine.Client.SetKadEnabledAsync(true);
        Check(await engine.Client.GetKadEnabledAsync(), "Kad preference enabled");
        NetworkState? kadState = null;
        for (int i = 0; i < 20; i++)
        {
            kadState = await engine.Client.GetNetworkStateAsync();
            if (kadState.KadRunning || kadState.KadConnected) break;
            await Task.Delay(100);
        }
        Check(kadState is { KadRunning: true } or { KadConnected: true }, "Kad start leaves the network running");
        await engine.Client.SetKadEnabledAsync(false);
        Check(!(await engine.Client.GetKadEnabledAsync()), "Kad preference disabled again");
        Check(!(await engine.Client.GetNetworkStateAsync()).KadRunning && !(await engine.Client.GetNetworkStateAsync()).KadConnected, "Kad stop leaves the network inactive");
        var testServer = await engine.Client.AddServerAsync("203.0.113.31", "4661", "Persistencia de servidor");
        Check((await engine.Client.GetServersAsync()).Any(s => s.Endpoint == testServer.Endpoint && s.Name == testServer.Name), "add and list real saved server");
        await engine.Client.AddServerAsync("203.0.113.31", "4661", "Duplicate");
        Check((await engine.Client.GetServersAsync()).Count(s => s.Endpoint == testServer.Endpoint) == 1, "adding duplicate is idempotent");
        bool invalidPort = false;
        try { await engine.Client.AddServerAsync("127.0.0.1", "70000", "invalid"); } catch (ArgumentException) { invalidPort = true; }
        Check(invalidPort, "reject out-of-range port without changing engine");
        Check(!(await engine.Client.GetNetworkStateAsync()).Connected, "saved server is not connected automatically");

        // Controlled eD2k handshake, not a public server or a file-transfer test.
        // Only this disposable test profile permits LAN servers.
        await engine.Client.RequestAsync(new(0x40, EcTag.Integer(4, 2), new EcTag(0x1c00, 1, [], EcTag.Integer(0x1c07, 0))));
        // aMule rejects loopback even with LAN filtering disabled; bind this host's LAN address.
        var localAddress = (await Dns.GetHostAddressesAsync(Dns.GetHostName())).First(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && (ip.GetAddressBytes()[0] == 10 || (ip.GetAddressBytes()[0] == 192 && ip.GetAddressBytes()[1] == 168) || (ip.GetAddressBytes()[0] == 172 && ip.GetAddressBytes()[1] is >= 16 and <= 31)));
        var ed2kListener = new TcpListener(localAddress, 0); ed2kListener.Start();
        int ed2kPort = ((IPEndPoint)ed2kListener.LocalEndpoint).Port;
        using var handshakeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            var localServer = await engine.Client.AddServerAsync(localAddress.ToString(), ed2kPort.ToString(), "Local test server");
            var accepted = ed2kListener.AcceptTcpClientAsync(handshakeTimeout.Token).AsTask();
            await engine.Client.ConnectServerAsync(localServer);
            using var serverPeer = await accepted;
            var peerStream = serverPeer.GetStream();
            byte[] ed2kHeader = new byte[5]; await peerStream.ReadExactlyAsync(ed2kHeader, handshakeTimeout.Token);
            uint loginLength = BinaryPrimitives.ReadUInt32LittleEndian(ed2kHeader.AsSpan(1));
            Check(loginLength is > 0 and < 65536 && ed2kHeader[0] == 0xe3, "engine opens real eD2k TCP session");
            byte[] login = new byte[loginLength]; await peerStream.ReadExactlyAsync(login, handshakeTimeout.Token);
            Check(login[0] == 1, "eD2k login packet received by controlled server");
            var beforeId = await engine.Client.GetNetworkStateAsync();
            Check(beforeId.Connecting && !beforeId.Connected, "TCP alone is not reported as connected eD2k");
            await peerStream.WriteAsync(Convert.FromHexString("E3050000004001000012"), handshakeTimeout.Token);
            NetworkState? connected = null;
            for (int i = 0; i < 40; i++) { connected = await engine.Client.GetNetworkStateAsync(); if (connected.Connected) break; await Task.Delay(50); }
            Check(connected is { Connected: true, ClientId: 0x12000001 } && connected.Server?.Port == ed2kPort, "confirmed eD2k connection and HighID decoded");
            await engine.Client.StartSearchAsync("amule-modern-search-test");
            byte[] searchRequest;
            do
            {
                await peerStream.ReadExactlyAsync(ed2kHeader, handshakeTimeout.Token);
                searchRequest = new byte[BinaryPrimitives.ReadUInt32LittleEndian(ed2kHeader.AsSpan(1))];
                await peerStream.ReadExactlyAsync(searchRequest, handshakeTimeout.Token);
            } while (searchRequest[0] != 0x16);
            Check(System.Text.Encoding.UTF8.GetString(searchRequest).Contains("amule-modern-search-test"), "real eD2k server receives search terms");
            const string searchHash = "B448017AAF21D8525FC10AE87AA6729D";
            using var resultBytes = new MemoryStream();
            using (var writer = new BinaryWriter(resultBytes, System.Text.Encoding.UTF8, true))
            {
                writer.Write((byte)0x33); writer.Write(1u); writer.Write(Convert.FromHexString(searchHash));
                writer.Write(0u); writer.Write((ushort)0); writer.Write(3u);
                writer.Write((byte)2); writer.Write((ushort)1); writer.Write((byte)1);
                var nameBytes = System.Text.Encoding.UTF8.GetBytes("amule-modern-search-test.txt"); writer.Write((ushort)nameBytes.Length); writer.Write(nameBytes);
                writer.Write((byte)3); writer.Write((ushort)1); writer.Write((byte)2); writer.Write(3u);
                writer.Write((byte)3); writer.Write((ushort)1); writer.Write((byte)0x15); writer.Write(7u);
            }
            byte[] resultHeader = new byte[5]; resultHeader[0] = 0xe3;
            BinaryPrimitives.WriteUInt32LittleEndian(resultHeader.AsSpan(1), (uint)resultBytes.Length);
            await peerStream.WriteAsync(resultHeader, handshakeTimeout.Token); await peerStream.WriteAsync(resultBytes.ToArray(), handshakeTimeout.Token);
            IReadOnlyList<SearchResult> searchResults = [];
            for (int i = 0; i < 40; i++) { searchResults = await engine.Client.GetSearchResultsAsync(); if (searchResults.Count > 0) break; await Task.Delay(50); }
            Check(searchResults.Single() is { Name: "amule-modern-search-test.txt", Size: 3, Sources: 7 } && searchResults[0].Hash == searchHash, "real server result decoded through engine EC");
            await engine.Client.DownloadSearchResultAsync(searchHash);
            Check((await engine.Client.GetDownloadsAsync()).Any(d => d.Hash == searchHash), "search result added to real download queue");
            await engine.Client.PauseAsync(searchHash, true);
            await engine.Client.StopSearchAsync();
            Check((await engine.Client.GetSearchResultsAsync()).Count == 1, "stop search preserves received results");
            await engine.Client.DisconnectServerAsync();
            Check(!(await engine.Client.GetNetworkStateAsync()).Connected, "disconnect established eD2k session");
        }
        finally { ed2kListener.Stop(); }
    }
    Check(true, "graceful shutdown without kill");
    await using (var restarted = new EngineSession())
    {
        await restarted.StartAsync(root, profile);
        var persisted = (await restarted.Client.GetDownloadsAsync()).Single(d => d.Hash == hash);
        Check(persisted.Hash == hash && persisted.State == 7, "queue and paused state survive restart");
        Check(!(await restarted.Client.GetDownloadsAsync()).Any(d => d.Hash == "C448017AAF21D8525FC10AE87AA6729D"), "cancelled download does not return after restart");
        Check((await restarted.Client.GetServersAsync()).Any(s => s.Address == "203.0.113.31" && s.Port == 4661), "saved server list survives restart");
        Check(!(await restarted.Client.GetNetworkStateAsync()).Connected && !(await restarted.Client.GetNetworkStateAsync()).Connecting, "restart does not autoconnect");
        Check(!(await restarted.Client.GetKadEnabledAsync()) && !(await restarted.Client.GetNetworkStateAsync()).KadRunning, "restart keeps Kad disabled");
        Check((await restarted.Client.GetSharedFilesAsync()).Any(f => f.Name == incomingShare), "incoming shared file survives restart");
        Check(!restarted.ExtraSharedDirectories().Any(d => UserFolders.PathsEqual(d, extraDir)), "removed extra share does not return after restart");
        Check(File.Exists(Path.Combine(restarted.ProfilePath, "Temp", "001.part.met")), "Windows temporary files use the intended profile directory");
        Check(UserFolders.IsUnder(restarted.IncomingPath, restarted.ProfilePath) && UserFolders.IsUnder(restarted.TempPath, restarted.ProfilePath), "isolated profiles do not use the user Downloads library");
    }
    Console.WriteLine("NOTE: controlled same-host LAN eD2k handshake verified. No public server or file payload tested in this suite.");
}
Console.WriteLine($"RESULT: {passed} checks passed.");
