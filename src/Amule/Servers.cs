using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace AmuleModern.Amule;

public sealed record ServerItem(string Address, ushort Port, string Name, ulong Users, ulong Files, ulong Ping)
{
    public string Endpoint => $"{Address}:{Port}";
    public string UsersText => Users == 0 ? "—" : Users.ToString("N0");
    public string FilesText => Files == 0 ? "—" : Files.ToString("N0");
    public string PingText => Ping == 0 ? "—" : $"{Ping} ms";
    public EcTag ToTag()
    {
        byte[] data = new byte[6];
        IPAddress.Parse(Address).GetAddressBytes().CopyTo(data, 0);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), Port);
        return new(0x500, 8, data);
    }
    public static ServerItem FromTag(EcTag tag)
    {
        if (tag.Type != 8 || tag.Data.Length != 6) throw new InvalidDataException("Dirección de servidor EC inválida.");
        string address = new IPAddress(tag.Data.AsSpan(0, 4)).ToString();
        ushort port = BinaryPrimitives.ReadUInt16BigEndian(tag.Data.AsSpan(4));
        return new(address, port, tag.Find(0x501)?.String ?? address, tag.Find(0x505)?.Number ?? 0, tag.Find(0x507)?.Number ?? 0, tag.Find(0x504)?.Number ?? 0);
    }
}

public sealed record NetworkState(bool Connected, bool Connecting, bool KadConnected, bool KadRunning, ulong? ClientId, ServerItem? Server)
{
    public string Ed2kText => Connected ? $"Conectado · {(ClientId is null ? "ID desconocida" : ClientId < 16777216 ? "LowID" : "HighID")}" : Connecting ? "Conectando…" : "Desconectado";
    public string KadText => KadConnected ? "Conectado" : KadRunning ? "Conectando" : "Inactivo";
    public static NetworkState FromTag(EcTag tag)
    {
        ulong flags = tag.Number;
        var server = tag.Find(0x500);
        return new((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 16) != 0,
            tag.Find(6)?.Number, server is null ? null : ServerItem.FromTag(server));
    }
}

public sealed partial class EcClient
{
    public async Task EnableEd2kAsync(CancellationToken token = default)
    {
        // FULL + explicit boolean: preserve all omitted preferences (Kad, autoconnect, ports...).
        await RequestAsync(new(0x40, EcTag.Integer(4, 2), new EcTag(0x1300, 1, [], EcTag.Integer(0x130d, 1))), token);
    }
    public async Task<IReadOnlyList<ServerItem>> GetServersAsync(CancellationToken token = default)
    {
        var packet = await RequestAsync(new(0x2c, EcTag.Integer(4, 2)), token);
        if (packet.Operation != 0x2d) throw new InvalidDataException("Respuesta de servidores inesperada.");
        return packet.Tags.Where(t => t.Name == 0x500).Select(ServerItem.FromTag).ToArray();
    }
    public async Task<NetworkState> GetNetworkStateAsync(CancellationToken token = default)
    {
        var packet = await RequestAsync(new(0xb, EcTag.Integer(4, 2)), token);
        return NetworkState.FromTag(packet.Find(5) ?? throw new InvalidDataException("Falta el estado de red."));
    }
    public async Task<ServerItem> AddServerAsync(string address, string portText, string name, CancellationToken token = default)
    {
        address = address.Trim(); name = name.Trim();
        if (!ushort.TryParse(portText.Trim(), out ushort port) || port == 0) throw new ArgumentException("El puerto debe estar entre 1 y 65535.");
        if (string.IsNullOrWhiteSpace(address) || address.Length > 253 || Uri.CheckHostName(address) is UriHostNameType.Unknown or UriHostNameType.IPv6)
            throw new ArgumentException("Introduce una dirección IPv4 o un dominio, sin http:// ni puerto.");
        if (name.Length > 120 || name.Any(char.IsControl)) throw new ArgumentException("El nombre debe tener como máximo 120 caracteres y no contener saltos de línea.");
        IPAddress? ip;
        if (IPAddress.TryParse(address, out var parsed)) ip = parsed;
        else
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            try { ip = (await Dns.GetHostAddressesAsync(address, AddressFamily.InterNetwork, deadline.Token)).FirstOrDefault(); }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException) { throw new ArgumentException("No se pudo resolver el dominio a IPv4.", ex); }
        }
        if (ip is null || ip.AddressFamily != AddressFamily.InterNetwork || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast) || ip.GetAddressBytes()[0] >= 224)
            throw new ArgumentException("La dirección IPv4 del servidor no es válida.");
        var item = new ServerItem(ip.ToString(), port, name.Length == 0 ? address : name, 0, 0, 0);
        var existing = (await GetServersAsync(token)).FirstOrDefault(s => s.Endpoint == item.Endpoint);
        if (existing != null) return existing; // Idempotent add; do not duplicate the engine's list.
        await RequestAsync(new(0x31, EcTag.Text(0x503, item.Endpoint), EcTag.Text(0x501, item.Name)), token);
        return item;
    }
    public Task<EcPacket> ConnectServerAsync(ServerItem server, CancellationToken token = default) => RequestAsync(new(0x2f, server.ToTag()), token);
    public async Task DisconnectServerAsync(CancellationToken token = default)
    {
        // Pinned aMule disconnects established sessions; it does not cancel connection attempts.
        await RequestAsync(new(0x2e), token);
    }
}
