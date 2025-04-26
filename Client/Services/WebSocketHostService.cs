using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Client.Services;

public sealed class WebSocketHostService(int port) : IReportingService
{
    public const int StartingPort   = 8970;
    public const int CheckPortCount = 1029;

    public bool Started { get; private set; }

    private HttpListener?         HttpListener         { get; set; }
    private PlayerTrackerService? PlayerTrackerService { get; set; }

    private HashSet<HttpListenerWebSocketContext> Clients { get; } = [];

    public Task StartClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        // Execute netsh http add urlacl url=http://+:port/ user=DOMAIN\user as admin
        if (!AddressUtility.CheckUrlReservation(port))
        {
            progress.Report(new LogItem($"Reserving port {port} for websocket...", LogItem.LogType.Info));

            if (!AddressUtility.AddUrlReservation(port))
                progress.Report(new LogItem("Failed to reserve port. Websocket connection may fail.", LogItem.LogType.Error));
        }

        progress.Report(new LogItem($"Opening websocket...", LogItem.LogType.Info));

        HttpListener = new HttpListener();
        HttpListener.Prefixes.Add($"http://+:{port}/");
        HttpListener.Start();

        ListenerMainLoop(progress, cancellationToken).ConfigureAwait(false);

        Started = true;
        progress.Report(new LogItem($"Listening for websocket connections at *:{port}...", LogItem.LogType.Info));
        return Task.CompletedTask;
    }

    private async Task ListenerMainLoop(IProgress<LogItem> progress, CancellationToken cancellationToken = default)
    {
        while (HttpListener?.IsListening ?? false)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var context = await HttpListener.GetContextAsync();

            context.Response.AppendHeader("Access-Control-Allow-Origin", "*");

            if (context.Request.IsWebSocketRequest)
            {
                var socket = await context.AcceptWebSocketAsync(subProtocol: null);
                Clients.Add(socket);
                _ = Task.Run(() => HandleSocketAsync(progress, socket), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }

            await Task.Yield();
        }
    }

    private async Task HandleSocketAsync(IProgress<LogItem> progress, HttpListenerWebSocketContext socket)
    {
        try
        {
            var discordId = string.Empty;
            while (socket.WebSocket.State == WebSocketState.Open)
            {
                var buffer = new Memory<byte>(new byte[4096]);

                var received = await socket.WebSocket.ReceiveAsync(
                    buffer,
                    CancellationToken.None
                );

                switch (received.MessageType)
                {
                    case WebSocketMessageType.Text:
                    {
                        buffer = buffer[..received.Count];
                        if (buffer.Length == 0)
                            break;

                        discordId = DiscordUtility.GetFixedLengthUid(Encoding.ASCII.GetString(buffer.Span));
                        await InitializePlayers(socket);
                        break;
                    }
                    case WebSocketMessageType.Binary:
                    {
                        buffer = buffer[..received.Count];
                        if (buffer.Length == 0)
                            break;

                        await BroadcastPositionPacket(buffer, discordId);
                        break;
                    }
                    case WebSocketMessageType.Close:
                    {
                        await socket.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closed by client.",
                                                          CancellationToken.None);
                        Clients.Remove(socket);

                        await BroadcastDisconnectPacket(discordId);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                await Task.Yield();
            }
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }

        socket.WebSocket.Dispose();
    }

    private async Task InitializePlayers(HttpListenerWebSocketContext socket)
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

        await socket.WebSocket.SendAsync(ms.ToArray(), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public async Task BroadcastPositionPacket(Memory<byte> data, string discordId)
    {
        if (HttpListener == null)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Position);
            binaryWriter.Write(data.Span);
            binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
        }

        var memory = ms.ToArray();
        var tasks  = new List<Task>();
        foreach (var client in Clients)
            tasks.Add(client.WebSocket.SendAsync(memory, WebSocketMessageType.Binary, true, CancellationToken.None));
        await Task.WhenAll(tasks);
    }

    private async Task BroadcastDisconnectPacket(string discordId)
    {
        if (HttpListener == null)
            return;

        using var ms = new MemoryStream();
        await using (var binaryWriter = new BinaryWriter(ms))
        {
            binaryWriter.Write((byte)OpCode.Disconnect);
            binaryWriter.Write(Encoding.ASCII.GetBytes(discordId));
        }

        var memory = ms.ToArray();
        var tasks  = new List<Task>();
        foreach (var client in Clients)
            tasks.Add(client.WebSocket.SendAsync(memory, WebSocketMessageType.Binary, true, CancellationToken.None));
        await Task.WhenAll(tasks);
    }

    public Task StopClient(IProgress<LogItem> progress, CancellationToken cancellationToken)
    {
        try
        {
            Clients.Clear();
            HttpListener?.Close();
            HttpListener?.Stop();
            Started = false;
        }
        catch (Exception e)
        {
            progress.Report(new LogItem(e.Message, LogItem.LogType.Error, e.ToString()));
        }

        return Task.CompletedTask;
    }

    public enum OpCode : byte
    {
        InitPlayers,
        Position,
        Disconnect
    }
}
