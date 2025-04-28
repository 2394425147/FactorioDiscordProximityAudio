using System.Text;
using PHS.Networking.Enums;
using WebsocketsSimple.Server;
using WebsocketsSimple.Server.Events.Args;
using WebsocketsSimple.Server.Models;

namespace Client.Services;

public sealed class WebSocketHostService(int port) : IReportingService
{
    public const int StartingPort   = 8970;
    public const int CheckPortCount = 1029;

    public bool Started { get; private set; }

    private WebsocketServer?                         WebsocketServer      { get; set; }
    private PlayerTrackerService?                    PlayerTrackerService { get; set; }
    private IProgress<LogItem>?                      Progress             { get; set; }
    private Dictionary<string, ClientConnectionInfo> Clients              { get; } = [];

    public async Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Progress = progress;

        if (!AddressUtility.CheckUrlReservation(port))
        {
            progress.Report(new LogItem($"Reserving port {port} for websocket...", LogItem.LogType.Info));

            if (!AddressUtility.AddUrlReservation(port))
                progress.Report(new LogItem("Failed to reserve port. Websocket connection may fail.", LogItem.LogType.Error));
        }

        progress.Report(new LogItem("Opening websocket...", LogItem.LogType.Info));

        WebsocketServer                 =  new WebsocketServer(new ParamsWSServer(port));
        WebsocketServer.MessageEvent    += OnMessageSentOrReceived;
        WebsocketServer.ConnectionEvent += OnClientConnectionChanged;
        await WebsocketServer.StartAsync(cancellationToken);

        progress.Report(new LogItem($"Listening for connections at port {port}.", LogItem.LogType.Info));
        Started = true;
    }

    private void OnMessageSentOrReceived(object sender, WSMessageServerEventArgs args)
    {
        if (args.MessageEventType != MessageEventType.Receive)
            return;

        var buffer = new Memory<byte>(args.Bytes);
        var opCode = (WebSocketClientService.OpCode)buffer.Span[0];
        buffer = buffer[1..];

        switch (opCode)
        {
            case WebSocketClientService.OpCode.Identify:
            {
                var discordId = Encoding.ASCII.GetString(buffer.Span);
                Clients.TryAdd(args.Connection.ConnectionId, new ClientConnectionInfo
                {
                    discordId = discordId,
                    socketId  = args.Connection.ConnectionId
                });
                Task.Run(() => InitializePlayers(args));
                break;
            }
            case WebSocketClientService.OpCode.Update:
            {
                if (!Clients.TryGetValue(args.Connection.ConnectionId, out var value))
                    return;

                Task.Run(() => BroadcastPositionPacket(buffer, value.discordId));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(opCode), opCode, null);
        }
    }

    private void OnClientConnectionChanged(object sender, WSConnectionServerEventArgs args)
    {
        if (args.ConnectionEventType == ConnectionEventType.Disconnect)
        {
            if (!Clients.Remove(args.Connection.ConnectionId, out var info))
                return;

            Task.Run(() => BroadcastDisconnectPacket(info.discordId));
        }
    }

    private async Task InitializePlayers(WSMessageServerEventArgs args)
    {
        PlayerTrackerService ??= Program.GetService<PlayerTrackerService>();

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.InitPlayers);

            if (PlayerTrackerService != null && PlayerTrackerService.Clients.Count != 0)
            {
                foreach (var client in PlayerTrackerService.Clients)
                {
                    binaryWriter.Write(Encoding.ASCII.GetBytes(DiscordUtility.GetFixedLengthUid(client.DiscordId)));
                    binaryWriter.Write(client.X);
                    binaryWriter.Write(client.Y);
                    binaryWriter.Write(client.SurfaceIndex);
                }
            }
        }

        await WebsocketServer!.SendToConnectionAsync(ms.ToArray(), args.Connection);
    }

    public async Task BroadcastPositionPacket(Memory<byte> data, string discordId)
    {
        if (WebsocketServer == null)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Position);
            binaryWriter.Write(data.Span);
            binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
        }

        var memory = ms.ToArray();
        await WebsocketServer.BroadcastToAllConnectionsAsync(memory);
    }

    private async Task BroadcastDisconnectPacket(string discordId)
    {
        if (WebsocketServer == null)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Disconnect);
            binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
        }

        var memory = ms.ToArray();
        await WebsocketServer.BroadcastToAllConnectionsAsync(memory);
    }

    public async Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        try
        {
            Clients.Clear();
            await WebsocketServer!.StopAsync(cancellationToken);
            WebsocketServer.Dispose();
            Started = false;
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }

    public sealed class ClientConnectionInfo
    {
        public string discordId = string.Empty;
        public string socketId  = string.Empty;
    }

    public enum OpCode : byte
    {
        InitPlayers,
        Position,
        Disconnect
    }
}
