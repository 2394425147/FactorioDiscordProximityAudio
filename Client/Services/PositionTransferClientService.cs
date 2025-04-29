using System.Text;
using Client.Models;
using PHS.Networking.Enums;
using Serilog;
using WebsocketsSimple.Client;
using WebsocketsSimple.Client.Events.Args;
using WebsocketsSimple.Client.Models;

namespace Client.Services;

public sealed class PositionTransferClientService : IService
{
    public  bool                        Started             { get; private set; }
    private FactorioFileWatcherService? FactorioFileWatcher { get; set; }
    private DiscordPipeService?         DiscordPipe         { get; set; }
    private WebsocketClient?            WebSocket           { get; set; }

    public event OnAnyClientUpdateReceived? AnyClientUpdateReceived;
    public event OnAnyClientDisconnected?   AnyClientDisconnected;

    public delegate void OnAnyClientUpdateReceived(string discordId, FactorioPosition position);

    public delegate void OnAnyClientDisconnected(string discordId);

    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        DiscordPipe         = services.GetService(typeof(DiscordPipeService)) as DiscordPipeService;
        FactorioFileWatcher = services.GetService(typeof(FactorioFileWatcherService)) as FactorioFileWatcherService;
        DiscordPipe         = services.GetService(typeof(DiscordPipeService)) as DiscordPipeService;

        try
        {
            Log.Information("Connecting to {TargetIp}:{TargetPort}...", Main.targetIp, Main.targetPort);

            WebSocket                 =  new WebsocketClient(new ParamsWSClient(Main.targetIp, Main.targetPort, false));
            WebSocket.ConnectionEvent += OnConnectionChanged;
            WebSocket.MessageEvent    += OnMessageSentOrReceived;
            await WebSocket.ConnectAsync(cancellationToken);

            FactorioFileWatcher!.OnPositionUpdated += OnLocalPositionUpdated;
            Log.Information("Connected to host.");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while connecting to proximity audio host: {Message}", e.Message);
            throw;
        }

        Started = true;
    }

    private void OnConnectionChanged(object sender, WSConnectionClientEventArgs args)
    {
        if (args.ConnectionEventType != ConnectionEventType.Connected)
            return;

        if (DiscordPipe?.LocalUser == null)
        {
            Log.Error("Discord handshake not established before websocket connection.");
            return;
        }

        Task.Run(() => SendIdentificationPacket(DiscordPipe.LocalUser.id));
    }

    private void OnMessageSentOrReceived(object sender, WSMessageClientEventArgs args)
    {
        try
        {
            if (args.MessageEventType != MessageEventType.Receive)
                return;

            var buffer = new Memory<byte>(args.Bytes);
            var opCode = (PositionTransferHostService.OpCode)buffer.Span[0];
            buffer = buffer[1..];

            switch (opCode)
            {
                case PositionTransferHostService.OpCode.InitPlayers:
                {
                    while (buffer.Length > 0)
                    {
                        var discordId =
                            DiscordUtility.GetUid(Encoding.ASCII.GetString(buffer.Span[..DiscordUtility.MaxUidLength]));

                        buffer = buffer[DiscordUtility.MaxUidLength..];

                        var x       = BitConverter.ToDouble(buffer[..8].Span);
                        var y       = BitConverter.ToDouble(buffer[8..16].Span);
                        var surface = BitConverter.ToInt32(buffer[16..20].Span);

                        AnyClientUpdateReceived?.Invoke(discordId, new FactorioPosition
                        {
                            surfaceIndex = surface,
                            x            = x,
                            y            = y
                        });

                        buffer = buffer[20..];
                    }

                    if (FactorioFileWatcher?.LastPositionPacket != null)
                        Task.Run(() => SendPositionPacket(FactorioFileWatcher.LastPositionPacket.Value));
                    break;
                }
                case PositionTransferHostService.OpCode.Position:
                {
                    var x         = BitConverter.ToDouble(buffer[..8].Span);
                    var y         = BitConverter.ToDouble(buffer[8..16].Span);
                    var surface   = BitConverter.ToInt32(buffer[16..20].Span);
                    var discordId = Encoding.ASCII.GetString(buffer[20..].Span);

                    AnyClientUpdateReceived?.Invoke(DiscordUtility.GetUid(discordId), new FactorioPosition
                    {
                        surfaceIndex = surface,
                        x            = x,
                        y            = y
                    });
                    break;
                }
                case PositionTransferHostService.OpCode.Disconnect:
                {
                    var discordId = Encoding.ASCII.GetString(buffer.Span);
                    AnyClientDisconnected?.Invoke(DiscordUtility.GetUid(discordId));
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error processing proximity audio update: {Message}", e.Message);
        }
    }

    private async Task SendIdentificationPacket(string discordId)
    {
        try
        {
            using var ms = new MemoryStream();
            await using (var binaryWriter = new BinaryWriter(ms))
            {
                binaryWriter.Write((byte)OpCode.Identify);
                binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
            }

            await WebSocket!.SendAsync(ms.ToArray());
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error sending ID to proximity audio host: {Message}", e.Message);
        }
    }

    private async void OnLocalPositionUpdated(FactorioPosition obj)
    {
        try
        {
            await SendPositionPacket(obj);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error sending position to proximity audio host: {Message}", e.Message);
        }
    }

    private async Task SendPositionPacket(FactorioPosition obj)
    {
        try
        {
            if (DiscordPipe?.LocalUser == null || WebSocket == null)
                return;

            using var ms = new MemoryStream();
            await using (var binaryWriter = new BinaryWriter(ms))
            {
                binaryWriter.Write((byte)OpCode.Update);
                binaryWriter.Write(obj.x);
                binaryWriter.Write(obj.y);
                binaryWriter.Write(obj.surfaceIndex);
            }

            await WebSocket.SendAsync(ms.ToArray());
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
            if (WebSocket != null)
                await WebSocket.DisconnectAsync(cancellationToken: cancellationToken);
            WebSocket?.Dispose();
            Started = false;
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
        }
    }

    public enum OpCode : byte
    {
        Identify,
        Update
    }
}
