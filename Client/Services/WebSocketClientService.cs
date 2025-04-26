using System.Net.WebSockets;
using System.Text;
using Client.Models;
using PHS.Networking.Enums;
using WebsocketsSimple.Client;
using WebsocketsSimple.Client.Events.Args;
using WebsocketsSimple.Client.Models;

namespace Client.Services;

public sealed class WebSocketClientService(string address, int port) : IReportingService
{
    public  bool                        Started             { get; private set; }
    private IProgress<LogItem>?         Progress            { get; set; }
    private FactorioFileWatcherService? FactorioFileWatcher { get; set; }
    private DiscordNamedPipeService?    DiscordNamedPipe    { get; set; }
    private WebsocketClient?            WebSocket           { get; set; }

    public event OnAnyClientUpdateReceived? AnyClientUpdateReceived;
    public event OnAnyClientDisconnected?   AnyClientDisconnected;

    public delegate void OnAnyClientUpdateReceived(string discordId, FactorioPosition position);

    public delegate void OnAnyClientDisconnected(string discordId);

    public async Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        Progress            = progress;
        FactorioFileWatcher = Program.GetService<FactorioFileWatcherService>();
        DiscordNamedPipe    = Program.GetService<DiscordNamedPipeService>();

        if (FactorioFileWatcher == null || DiscordNamedPipe == null)
        {
            progress.Report(new LogItem($"{nameof(FactorioFileWatcherService)} or {nameof(DiscordNamedPipeService)} not found.",
                                        LogItem.LogType.Error));
            return;
        }

        try
        {
            progress.Report(new LogItem($"Connecting to {address}:{port}...", LogItem.LogType.Info));

            WebSocket                 =  new WebsocketClient(new ParamsWSClient(address, port, false));
            WebSocket.ConnectionEvent += OnConnectionChanged;
            WebSocket.MessageEvent    += OnMessageSentOrReceived;
            await WebSocket.ConnectAsync(cancellationToken);

            FactorioFileWatcher.OnPositionUpdated += OnLocalPositionUpdated;
            progress.Report(new LogItem($"Connected to {address}:{port}.", LogItem.LogType.Info));
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
            throw;
        }

        Started = true;
    }

    private void OnConnectionChanged(object sender, WSConnectionClientEventArgs args)
    {
        if (args.ConnectionEventType == ConnectionEventType.Connected)
        {
            if (DiscordNamedPipe?.HandshakePacket != null)
                Task.Run(() => SendIdentificationPacket(DiscordNamedPipe.HandshakePacket.data.user.id));
        }
    }

    private void OnMessageSentOrReceived(object sender, WSMessageClientEventArgs args)
    {
        if (args.MessageEventType != MessageEventType.Receive)
            return;

        var buffer = new Memory<byte>(args.Bytes);
        var opCode = (WebSocketHostService.OpCode)buffer.Span[0];
        buffer = buffer[1..];

        switch (opCode)
        {
            case WebSocketHostService.OpCode.InitPlayers:
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
            case WebSocketHostService.OpCode.Position:
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
            case WebSocketHostService.OpCode.Disconnect:
            {
                var discordId = Encoding.ASCII.GetString(buffer.Span);
                AnyClientDisconnected?.Invoke(DiscordUtility.GetUid(discordId));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(opCode), opCode, null);
        }
    }

    private async Task SendIdentificationPacket(string discordId)
    {
        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Identify);
            binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
        }

        await WebSocket!.SendAsync(ms.ToArray());
    }

    private async void OnLocalPositionUpdated(FactorioPosition obj)
    {
        try
        {
            await SendPositionPacket(obj);
        }
        catch (Exception e)
        {
            Progress?.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }

    private async Task SendPositionPacket(FactorioPosition obj)
    {
        if (DiscordNamedPipe?.HandshakePacket == null || WebSocket == null)
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

    public async Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
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
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }

    public enum OpCode : byte
    {
        Identify,
        Update
    }
}
