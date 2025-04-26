using System.Net.WebSockets;
using System.Text;
using Client.Models;

namespace Client.Services;

public sealed class WebSocketClientService(string address, int port) : IReportingService
{
    public  bool                        Started             { get; private set; }
    private IProgress<LogItem>?         Progress            { get; set; }
    private FactorioFileWatcherService? FactorioFileWatcher { get; set; }
    private DiscordNamedPipeService?    DiscordNamedPipe    { get; set; }
    private ClientWebSocket?            WebSocket           { get; set; }

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
            WebSocket = new ClientWebSocket();
            await WebSocket.ConnectAsync(new Uri($"ws://{address}:{port}"), cancellationToken);

            if (DiscordNamedPipe.HandshakePacket != null)
                await WebSocket.SendAsync(
                    Encoding.ASCII.GetBytes(DiscordNamedPipe.HandshakePacket.data.user.id),
                    WebSocketMessageType.Text, true, CancellationToken.None);

            FactorioFileWatcher.OnPositionUpdated += OnLocalPositionUpdated;
            progress.Report(new LogItem($"Connected to {address}:{port}.", LogItem.LogType.Info));

            _ = ListenerMainLoop(progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
            throw;
        }

        Started = true;
    }

    private async Task ListenerMainLoop(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        while (WebSocket?.State == WebSocketState.Open)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var buffer   = new Memory<byte>(new byte[4096]);
            var received = await WebSocket.ReceiveAsync(buffer, cancellationToken);

            if (received.MessageType == WebSocketMessageType.Close)
            {
                // await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken);
                break;
            }

            buffer = buffer[..received.Count];

            if (buffer.Length == 0)
                continue;

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
                        await SendPositionPacket(FactorioFileWatcher.LastPositionPacket.Value);
                    continue;
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
                    continue;
                }
                case WebSocketHostService.OpCode.Disconnect:
                {
                    var discordId = Encoding.ASCII.GetString(buffer.Span);
                    AnyClientDisconnected?.Invoke(DiscordUtility.GetUid(discordId));
                    continue;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
            Progress?.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }

    private async Task SendPositionPacket(FactorioPosition obj)
    {
        if (DiscordNamedPipe?.HandshakePacket == null || WebSocket?.State != WebSocketState.Open)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write(obj.x);
            binaryWriter.Write(obj.y);
            binaryWriter.Write(obj.surfaceIndex);
        }

        await WebSocket.SendAsync(ms.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        if (FactorioFileWatcher != null)
            FactorioFileWatcher.OnPositionUpdated -= OnLocalPositionUpdated;

        try
        {
            if (WebSocket?.State == WebSocketState.Open)
                await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing.", CancellationToken.None);
            WebSocket?.Dispose();
            Started = false;
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }
    }
}
