using AmuleModern.Amule;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

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
Check(EcClient.Md5Hex("abc") == "900150983cd24fb0d6963f7d28e17f72", "MD5 known vector");

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
    await using (var engine = new EngineSession())
    {
        await engine.StartAsync(root, profile);
        Check(engine.ProcessId.HasValue, "real amuled process and EC auth");
        Console.WriteLine($"ENGINE: {engine.Client.ServerVersion}; EC: 127.0.0.1:{engine.Port}");
        using (var wrong = new EcClient())
        {
            bool rejected = false;
            try { await wrong.ConnectAsync(engine.Port, EcClient.Md5Hex("wrong")); }
            catch (UnauthorizedAccessException) { rejected = true; }
            Check(rejected, "real engine rejects wrong password");
        }
        Check((await engine.Client.GetDownloadsAsync()).Count == 0, "isolated empty queue");
        await engine.Client.AddLinkAsync(link);
        var downloads = await engine.Client.GetDownloadsAsync();
        Check(downloads.Any(d => d.Hash == hash && d.Size == 3), "add link and read real queue");
        await engine.Client.PauseAsync(hash, true);
        Check((await engine.Client.GetDownloadsAsync()).Single().State == 7, "pause real download");
        await engine.Client.PauseAsync(hash, false);
        Check((await engine.Client.GetDownloadsAsync()).Single().State != 7, "resume real download");
        await engine.Client.PauseAsync(hash, true);
        Check((await engine.Client.RequestAsync(new(0x0a, EcTag.Integer(4, 0)))).Operation == 0x0c, "real stats request");
    }
    Check(true, "graceful shutdown without kill");
    await using (var restarted = new EngineSession())
    {
        await restarted.StartAsync(root, profile);
        var persisted = (await restarted.Client.GetDownloadsAsync()).Single();
        Check(persisted.Hash == hash && persisted.State == 7, "queue and paused state survive restart");
    }
    Console.WriteLine("NOTE: networking disabled. No P2P payload transfer tested yet.");
}
Console.WriteLine($"RESULT: {passed} checks passed.");
