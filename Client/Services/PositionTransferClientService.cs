using System.Collections.Concurrent;
using System.Text;
using Client.Models;
using ENet;
using Serilog;

namespace Client.Services;

public sealed class PositionTransferClientService : IService
{
    private FactorioFileWatcherService? FactorioFileWatcher { get; set; }
    private DiscordPipeService?         DiscordPipe         { get; set; }
    private Host?                       Client              { get; set; }
    private Thread?                     ENetThread          { get; set; }
    private bool                        ShuttingDown        { get; set; }

    public ConcurrentDictionary<uint, string> PeerToDiscordId { get; } = new();
    public event OnAnyClientUpdateReceived?   AnyClientUpdateReceived;
    public event OnAnyClientDisconnected?     AnyClientDisconnected;

    public delegate void OnAnyClientUpdateReceived(string discordId, ClientPosition position);

    public delegate void OnAnyClientDisconnected(string discordId);

    private Peer? _server;

    public Task<bool> StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        DiscordPipe         = services.GetService(typeof(DiscordPipeService)) as DiscordPipeService;
        FactorioFileWatcher = services.GetService(typeof(FactorioFileWatcherService)) as FactorioFileWatcherService;

        try
        {
            if (DiscordPipe?.LocalUser == null)
            {
                Log.Error("Discord handshake not established before websocket connection.");
                return Task.FromResult(false);
            }

            Log.Information("Connecting to {TargetIp}:{TargetPort}...", Main.targetIp, Main.targetPort);

            Client = new Host();
            var address = new Address
            {
                Port = Main.targetPort
            };
            address.SetIP(Main.targetIp);

            var channelLimit = Enum.GetValues<ChannelType>().Length;
            Client.Create(null, 1, channelLimit);
            _server = Client.Connect(address, channelLimit);

            ENetThread = new Thread(MainLoop);
            ENetThread.Start();

            FactorioFileWatcher!.OnPositionUpdated += OnLocalPositionUpdated;
            Log.Information("Started client.");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while connecting to proximity audio host: {Message}", e.Message);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private void MainLoop()
    {
        while (Client is { IsSet: true } && !ShuttingDown)
        {
            // Keep processing events until ...
            if (Client.CheckEvents(out var netEvent) <= 0)
            {
                // No events to process, look for packets to turn into events
                // ENet runs on a single thread, if timeout is 0, it'll use 100% of the CPU
                if (Client.Service(15, out netEvent) <= 0)
                    continue;
            }

            switch (netEvent.Type)
            {
                case EventType.None:
                    break;

                case EventType.Connect:
                    Log.Information("Successfully connected to server.");
                    SendIdentificationPacket(DiscordPipe!.LocalUser!.id);
                    break;

                case EventType.Disconnect:
                    Log.Information("Disconnected from server.");
                    break;

                case EventType.Timeout:
                    Log.Warning("Connection timed out.");
                    break;

                case EventType.Receive:
                    OnMessageReceived(ref netEvent);
                    netEvent.Packet.Dispose();
                    break;
            }
        }
    }

    private unsafe void OnMessageReceived(ref Event netEvent)
    {
        var buffer = new Span<byte>((byte*)netEvent.Packet.Data, netEvent.Packet.Length);
        var opCode = (ChannelType)netEvent.ChannelID;

        switch (opCode)
        {
            case ChannelType.Identify:
            {
                while (buffer.Length > 0)
                {
                    var clientId = BitConverter.ToUInt32(buffer[..sizeof(uint)]);
                    buffer = buffer[sizeof(uint)..];
                    var discordId = DiscordUtility.GetUid(Encoding.ASCII.GetString(buffer[..DiscordUtility.MaxUidLength]));
                    buffer = buffer[DiscordUtility.MaxUidLength..];

                    PeerToDiscordId[clientId] = discordId;
                }

                break;
            }
            case ChannelType.Position:
            {
                var x       = BitConverter.ToDouble(buffer[..8]);
                var y       = BitConverter.ToDouble(buffer[8..16]);
                var surface = BitConverter.ToInt32(buffer[16..20]);
                var peerId  = BitConverter.ToUInt32(buffer[20..]);

                AnyClientUpdateReceived?.Invoke(PeerToDiscordId[peerId], new ClientPosition
                {
                    x            = x,
                    y            = y,
                    surfaceIndex = surface
                });
                break;
            }
            case ChannelType.Disconnect:
            {
                var peerId = BitConverter.ToUInt32(buffer);
                AnyClientDisconnected?.Invoke(PeerToDiscordId[peerId]);
                break;
            }
        }
    }

    private void SendIdentificationPacket(string discordId)
    {
        try
        {
            var bytes = Encoding.ASCII.GetBytes(DiscordUtility.GetFixedLengthUid(discordId));

            using var ms = new MemoryStream(bytes.Length);
            using (var binaryWriter = new BinaryWriter(ms))
                binaryWriter.Write(bytes);

            var packet = new Packet();
            packet.Create(ms.GetBuffer());
            _server?.Send((byte)ChannelType.Identify, ref packet);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error sending ID to proximity audio host: {Message}", e.Message);
        }
    }

    private void OnLocalPositionUpdated()
    {
        try
        {
            if (Client == null || FactorioFileWatcher is not { LastKnownPosition: not null })
                return;

            var position = FactorioFileWatcher.LastKnownPosition.Value;

            using var ms = new MemoryStream(sizeof(double) * 2 + sizeof(int));
            using (var binaryWriter = new BinaryWriter(ms))
            {
                binaryWriter.Write(position.x);
                binaryWriter.Write(position.y);
                binaryWriter.Write(position.surfaceIndex);
            }

            var packet = new Packet();
            packet.Create(ms.GetBuffer());
            _server?.Send((byte)ChannelType.Position, ref packet);

            if (Main.useVerboseLogging)
                Log.Information("Sent ({X:F2},{Y:F2},{Surface}) to proximity audio host.",
                                position.x, position.y, position.surfaceIndex);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error sending position to proximity audio host: {Message}", e.Message);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (FactorioFileWatcher != null)
            FactorioFileWatcher.OnPositionUpdated -= OnLocalPositionUpdated;

        try
        {
            ShuttingDown = true;

            while (ENetThread is { IsAlive: true })
                await Task.Delay(15, cancellationToken);

            if (Client != null)
            {
                Client.PreventConnections(true);
                _server?.Disconnect(0);
                Client.Flush();
                Client?.Dispose();
                Client = null;
            }

            ShuttingDown = false;
            Log.Information("Terminated proximity audio client.");
        }
        catch (Exception e)
        {
            Log.Error(e, "Error disconnecting from proximity audio host: {Message}", e.Message);
        }
    }
}
