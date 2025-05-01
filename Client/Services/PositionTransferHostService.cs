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
            Host.Server.Server.NoDelay     = true;
            Host.Server.Server.SendTimeout = 500;

            _ = Task.Run(() => PingClients(cancellationToken), cancellationToken);

            Log.Information("Listening for connections at port {TargetPort}.", Main.targetPort);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error opening websocket: {Message}", e.Message);
            return false;
        }

        return true;
    }

    private async Task PingClients(CancellationToken cancellationToken)
    {
        while (Host is { IsServerRunning: true } && cancellationToken.IsCancellationRequested == false)
        {
            try
            {
                if (Host.ConnectionCount > 0)
                {
                    if (Main.useVerboseLogging)
                        Log.Information("Pinging clients...");

                    await Host.BroadcastToAllConnectionsAsync([(byte)OpCode.Ping], cancellationToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error pinging clients: {Message}", e.Message);
            }
        }
    }

    private void OnClientConnectionChanged(object sender, WSConnectionServerEventArgs args)
    {
        switch (args.ConnectionEventType)
        {
            case ConnectionEventType.Connected:
                Clients.TryAdd(args.Connection.ConnectionId, new ClientConnectionInfo(args.Connection));
                Log.Information("Client {ValueDiscordId} connected.", args.Connection.ConnectionId);
                break;
            case ConnectionEventType.Disconnect:
            {
                if (!Clients.Remove(args.Connection.ConnectionId, out var info) || info.discordId == null)
                    return;

                if (ShuttingDown)
                    return;

                Log.Information("Client {ValueDiscordId} disconnected.", info.discordId);
                Task.Run(() => BroadcastClientDisconnectPacket(info.discordId));
                break;
            }
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
                if (Main.useVerboseLogging && Clients.TryGetValue(args.Connection.ConnectionId, out var client))
                    Log.Information("Pong from {Identifier}.", client.Identifier);
                break;
            case PositionTransferClientService.OpCode.Identify:
            {
                var discordId = Encoding.ASCII.GetString(buffer.Span);

                if (!Clients.TryGetValue(args.Connection.ConnectionId, out var clientInfo))
                {
                    clientInfo = new ClientConnectionInfo(args.Connection);
                    Clients.Add(args.Connection.ConnectionId, clientInfo);
                }

                clientInfo.discordId = discordId;
                Log.Information("Client {DiscordId} connected.", discordId);
                Task.Run(() => InitializePlayers(discordId, args.Connection));
                break;
            }
            case PositionTransferClientService.OpCode.Update:
            {
                if (!Clients.TryGetValue(args.Connection.ConnectionId, out var value))
                    return;

                Task.Run(() => BroadcastPositionPacket(buffer, value.Identifier));
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

    public async Task BroadcastPositionPacket(Memory<byte> data, string senderDiscordId)
    {
        if (Host == null)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Position);
            binaryWriter.Write(data.Span);
            binaryWriter.Write(Encoding.ASCII.GetBytes(senderDiscordId));
        }

        var memory = ms.ToArray();

        await Host.BroadcastToAllConnectionsAsync(memory);

        if (Main.useVerboseLogging)
            Log.Information("Broadcasted position from {DiscordId}.", senderDiscordId);
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

    private sealed class ClientConnectionInfo(ConnectionWSServer connection)
    {
        public string Identifier => discordId ?? connection.ConnectionId;

        public readonly ConnectionWSServer connection = connection;
        public          string?            discordId;
    }

    public enum OpCode : byte
    {
        Ping        = 200,
        InitPlayers = 201,
        Position    = 202,
        Disconnect  = 203
    }
}
