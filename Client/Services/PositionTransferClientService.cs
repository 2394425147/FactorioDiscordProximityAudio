using System.Net.WebSockets;
using System.Text;
using Client.Models;
using Serilog;
using Websocket.Client;

namespace Client.Services;

public sealed class PositionTransferClientService : IService
{
    private FactorioFileWatcherService? FactorioFileWatcher { get; set; }
    private DiscordPipeService?         DiscordPipe         { get; set; }
    private WebsocketClient?            WebSocket           { get; set; }

    public event OnAnyClientUpdateReceived? AnyClientUpdateReceived;
    public event OnAnyClientDisconnected?   AnyClientDisconnected;

    public delegate void OnAnyClientUpdateReceived(string discordId, FactorioPosition position);

    public delegate void OnAnyClientDisconnected(string discordId);

    public async Task<bool> StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        DiscordPipe         = services.GetService(typeof(DiscordPipeService)) as DiscordPipeService;
        FactorioFileWatcher = services.GetService(typeof(FactorioFileWatcherService)) as FactorioFileWatcherService;
        DiscordPipe         = services.GetService(typeof(DiscordPipeService)) as DiscordPipeService;

        try
        {
            Log.Information("Connecting to {TargetIp}:{TargetPort}...", Main.targetIp, Main.targetPort);

            WebSocket                       = new WebsocketClient(new Uri($"ws://{Main.targetIp}:{Main.targetPort}"));
            WebSocket.ErrorReconnectTimeout = TimeSpan.FromSeconds(3);
            WebSocket.ReconnectionHappened.Subscribe(OnReconnectionHappened);
            WebSocket.MessageReceived.Subscribe(OnMessageReceived);
            WebSocket.DisconnectionHappened.Subscribe(OnDisconnectionHappened);
            WebSocket.IsTextMessageConversionEnabled = false;
            await WebSocket.Start();

            FactorioFileWatcher!.OnPositionUpdated += OnLocalPositionUpdated;
            Log.Information("Started client.");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while connecting to proximity audio host: {Message}", e.Message);
            return false;
        }

        return true;
    }

    private void OnReconnectionHappened(ReconnectionInfo info)
    {
        switch (info.Type)
        {
            case ReconnectionType.Initial:
            case ReconnectionType.Lost:
            case ReconnectionType.NoMessageReceived:
            case ReconnectionType.Error:
            case ReconnectionType.ByUser:
            case ReconnectionType.ByServer:
                Log.Information("Connected to {TargetIp}:{TargetPort}.", Main.targetIp, Main.targetPort);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (DiscordPipe?.LocalUser == null)
        {
            Log.Error("Discord handshake not established before websocket connection.");
            return;
        }

        Task.Run(() => SendIdentificationPacket(DiscordPipe.LocalUser.id));
    }

    private async void OnDisconnectionHappened(DisconnectionInfo info)
    {
        try
        {
            switch (info.Type)
            {
                case DisconnectionType.Exit:
                    if (Main.useVerboseLogging)
                        Log.Information("Disposed client websocket.");
                    break;
                case DisconnectionType.Lost:
                    Log.Error("Lost connection to {TargetIp}:{TargetPort}.", Main.targetIp, Main.targetPort);
                    break;
                case DisconnectionType.NoMessageReceived:
                    Log.Warning("Lost connection to {TargetIp}:{TargetPort} due to no message received.",
                                Main.targetIp, Main.targetPort);
                    break;
                case DisconnectionType.Error:
                    Log.Error("Error while connecting to {TargetIp}:{TargetPort}.", Main.targetIp, Main.targetPort);
                    break;
                case DisconnectionType.ByUser:
                    if (Main.useVerboseLogging)
                        Log.Information("Disconnected from {TargetIp}:{TargetPort}.", Main.targetIp, Main.targetPort);
                    break;
                case DisconnectionType.ByServer:
                    Log.Information("Proximity audio host closed connection.");
                    await WebSocket!.Stop(WebSocketCloseStatus.NormalClosure, "Closing connection.");
                    WebSocket?.Dispose();
                    WebSocket = null;
                    await Main.servicesMarshal.StopAsync();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Error while disconnecting from proximity audio host: {Message}", e.Message);
        }
    }

    private void OnMessageReceived(ResponseMessage response)
    {
        try
        {
            var buffer = new Memory<byte>(response.Binary);
            var opCode = (PositionTransferHostService.OpCode)buffer.Span[0];
            buffer = buffer[1..];

            switch (opCode)
            {
                case PositionTransferHostService.OpCode.Ping:
                {
                    SendPongPacket();
                    break;
                }
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
                    var discordId = DiscordUtility.GetUid(Encoding.ASCII.GetString(buffer[20..].Span));

                    if (discordId == DiscordPipe?.LocalUser?.id)
                        return;

                    AnyClientUpdateReceived?.Invoke(discordId, new FactorioPosition
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

    private void SendPongPacket()
    {
        WebSocket?.Send([(byte)OpCode.Pong]);
        if (Main.useVerboseLogging)
            Log.Information("Sent pong to proximity audio host.");
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

            WebSocket!.Send(ms.ToArray());
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
            if (Main.useVerboseLogging)
                Log.Information("Sent ({X:F2},{Y:F2},{Surface}) to proximity audio host.",
                                obj.x, obj.y, obj.surfaceIndex);
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

            WebSocket.Send(ms.ToArray());
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
            {
                if (WebSocket.IsRunning)
                    await WebSocket.StopOrFail(WebSocketCloseStatus.NormalClosure, "Closing connection.");

                WebSocket.Dispose();
            }

            Log.Information("Terminated proximity audio client.");
        }
        catch (Exception e)
        {
            Log.Error(e, "Error disconnecting from proximity audio host: {Message}", e.Message);
        }
    }

    public enum OpCode : byte
    {
        Pong     = 200,
        Identify = 201,
        Update   = 202
    }
}
