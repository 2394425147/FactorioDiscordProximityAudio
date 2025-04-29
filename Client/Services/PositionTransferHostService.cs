using System.Text;
using PHS.Networking.Enums;
using Serilog;
using WebsocketsSimple.Server;
using WebsocketsSimple.Server.Events.Args;
using WebsocketsSimple.Server.Models;

namespace Client.Services;

public sealed class PositionTransferHostService : IService
{
    public const int StartingPort   = 8970;
    public const int CheckPortCount = 1029;

    public bool Started { get; private set; }

    private WebsocketServer?                         WebsocketServer      { get; set; }
    private PlayerTrackerService?                    PlayerTrackerService { get; set; }
    private Dictionary<string, ClientConnectionInfo> Clients              { get; } = [];

    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        PlayerTrackerService = services.GetService(typeof(PlayerTrackerService)) as PlayerTrackerService;

        Log.Information("Opening websocket...");

        WebsocketServer                 =  new WebsocketServer(new ParamsWSServer(Main.targetPort));
        WebsocketServer.MessageEvent    += OnMessageSentOrReceived;
        WebsocketServer.ConnectionEvent += OnClientConnectionChanged;
        await WebsocketServer.StartAsync(cancellationToken);

        Log.Information("Listening for connections at port {TargetPort}.", Main.targetPort);
        Started = true;
    }

    private void OnMessageSentOrReceived(object sender, WSMessageServerEventArgs args)
    {
        if (args.MessageEventType != MessageEventType.Receive)
            return;

        var buffer = new Memory<byte>(args.Bytes);
        var opCode = (PositionTransferClientService.OpCode)buffer.Span[0];
        buffer = buffer[1..];

        switch (opCode)
        {
            case PositionTransferClientService.OpCode.Identify:
            {
                var discordId = Encoding.ASCII.GetString(buffer.Span);
                Clients.TryAdd(args.Connection.ConnectionId, new ClientConnectionInfo
                {
                    discordId = discordId,
                });
                Log.Information("Client {DiscordId} connected.", discordId);
                Task.Run(() => InitializePlayers(args));
                break;
            }
            case PositionTransferClientService.OpCode.Update:
            {
                if (!Clients.TryGetValue(args.Connection.ConnectionId, out var value))
                    return;

                Task.Run(() => BroadcastPositionPacket(args.Connection, buffer, value.discordId));
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

            Log.Information("Client {ValueDiscordId} disconnected.", info.discordId);
            Task.Run(() => BroadcastDisconnectPacket(info.discordId));
        }
    }

    private async Task InitializePlayers(WSMessageServerEventArgs args)
    {
        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.InitPlayers);

            if (PlayerTrackerService != null && PlayerTrackerService.Clients.Count != 0)
            {
                foreach (var client in PlayerTrackerService.Clients.Values)
                {
                    binaryWriter.Write(Encoding.ASCII.GetBytes(DiscordUtility.GetFixedLengthUid(client.DiscordId)));
                    binaryWriter.Write(client.Position.x);
                    binaryWriter.Write(client.Position.y);
                    binaryWriter.Write(client.Position.surfaceIndex);
                }
            }
        }

        await WebsocketServer!.SendToConnectionAsync(ms.ToArray(), args.Connection);
    }

    public async Task BroadcastPositionPacket(ConnectionWSServer sender, Memory<byte> data, string discordId)
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

        foreach (var connection in WebsocketServer.Connections)
        {
            if (connection == sender)
                return;

            await WebsocketServer.SendToConnectionAsync(memory, connection);
        }
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

    public async Task StopAsync(CancellationToken cancellationToken)
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
            Log.Fatal(e, "Error terminating proximity audio host: {Message}", e.Message);
        }
    }

    public sealed class ClientConnectionInfo
    {
        public string discordId = string.Empty;
    }

    public enum OpCode : byte
    {
        InitPlayers,
        Position,
        Disconnect
    }
}
