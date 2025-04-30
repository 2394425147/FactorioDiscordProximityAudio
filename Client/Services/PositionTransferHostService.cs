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

    private bool ShuttingDown { get; set; }

    private WebsocketServer?                         Host                 { get; set; }
    private VolumeUpdaterService?                    PlayerTrackerService { get; set; }
    private Dictionary<string, ClientConnectionInfo> Clients              { get; } = [];

    public async Task<bool> StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            PlayerTrackerService = services.GetService(typeof(VolumeUpdaterService)) as VolumeUpdaterService;

            Log.Information("Opening websocket...");

            Host = new WebsocketServer(new ParamsWSServer(Main.targetPort, onlyEmitBytes: true));

            Host.ConnectionEvent += OnClientConnectionChanged;
            Host.MessageEvent    += OnMessageReceived;

            await Host.StartAsync(cancellationToken);
            Log.Information("Listening for connections at port {TargetPort}.", Main.targetPort);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error opening websocket: {Message}", e.Message);
            return false;
        }

        return true;
    }

    private void OnClientConnectionChanged(object sender, WSConnectionServerEventArgs args)
    {
        switch (args.ConnectionEventType)
        {
            case ConnectionEventType.Connected:
                Task.Run(() => OnPong(args.Connection));
                break;
            case ConnectionEventType.Disconnect:
            {
                if (!Clients.Remove(args.Connection.ConnectionId, out var info))
                    return;

                if (ShuttingDown)
                    return;

                Log.Information("Client {ValueDiscordId} disconnected.", info.discordId);
                Task.Run(() => BroadcastClientDisconnectPacket(info.discordId));
                break;
            }
        }
    }

    private async void OnPong(ConnectionWSServer connection)
    {
        try
        {
            if (Main.useVerboseLogging && Clients.TryGetValue(connection.ConnectionId, out var client))
                Log.Information("Pong from {DiscordID}.", client.discordId);

            await Task.Delay(TimeSpan.FromSeconds(15));

            if (Host is not { IsServerRunning: true })
                return;

            await Host.SendToConnectionAsync([(byte)OpCode.Ping], connection);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error pinging client: {Message}", e.Message);
        }
    }

    private void OnMessageReceived(object sender, WSMessageServerEventArgs args)
    {
        if (args.MessageEventType != MessageEventType.Receive)
            return;

        var buffer = new Memory<byte>(args.Bytes);
        var opCode = (PositionTransferClientService.OpCode)buffer.Span[0];
        buffer = buffer[1..];

        switch (opCode)
        {
            case PositionTransferClientService.OpCode.Pong:
                Task.Run(() => OnPong(args.Connection));
                break;
            case PositionTransferClientService.OpCode.Identify:
            {
                var discordId = Encoding.ASCII.GetString(buffer.Span);
                Clients.TryAdd(args.Connection.ConnectionId, new ClientConnectionInfo(args.Connection, discordId));
                Log.Information("Client {DiscordId} connected.", discordId);
                Task.Run(() => InitializePlayers(discordId, args.Connection));
                break;
            }
            case PositionTransferClientService.OpCode.Update:
            {
                if (!Clients.TryGetValue(args.Connection.ConnectionId, out var value))
                    return;

                Task.Run(() => BroadcastPositionPacket(args.Connection, buffer, value.discordId));
                break;
            }
        }
    }

    private async Task InitializePlayers(string discordId, ConnectionWSServer connection)
    {
        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.InitPlayers);

            if (PlayerTrackerService != null && PlayerTrackerService.Clients.Count != 0)
                foreach (var client in PlayerTrackerService.Clients.Values)
                {
                    if (client.DiscordId == discordId)
                        continue;

                    binaryWriter.Write(Encoding.ASCII.GetBytes(DiscordUtility.GetFixedLengthUid(client.DiscordId)));
                    binaryWriter.Write(client.Position.x);
                    binaryWriter.Write(client.Position.y);
                    binaryWriter.Write(client.Position.surfaceIndex);
                }
        }

        if (Host != null)
            await Host.SendToConnectionAsync(ms.ToArray(), connection);
    }

    public async Task BroadcastPositionPacket(ConnectionWSServer connection, Memory<byte> data, string discordId)
    {
        if (Host == null)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Position);
            binaryWriter.Write(data.Span);
            binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
        }

        var memory = ms.ToArray();

        foreach (var clients in Clients.Values)
        {
            if (clients.connection == connection)
                continue;

            await Host.SendToConnectionAsync(memory, connection);
        }
    }

    private async Task BroadcastClientDisconnectPacket(string discordId)
    {
        if (Host == null)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Disconnect);
            binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
        }

        await Host.BroadcastToAllConnectionsAsync(ms.ToArray());
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            ShuttingDown = true;

            if (Host != null)
            {
                await Host.StopAsync(cancellationToken);
                Host?.Dispose();
            }

            Log.Information("Terminated proximity audio host.");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error terminating proximity audio host: {Message}", e.Message);
        }
        finally
        {
            ShuttingDown = false;
        }
    }

    private sealed class ClientConnectionInfo(ConnectionWSServer connection, string discordId)
    {
        public readonly string             discordId  = discordId;
        public readonly ConnectionWSServer connection = connection;
    }

    public enum OpCode : byte
    {
        Ping        = 200,
        InitPlayers = 201,
        Position    = 202,
        Disconnect  = 203
    }
}
